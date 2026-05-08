import json
import signal
import time
import socket
import threading
import hashlib
import os
import zmq
from multiprocessing import Process, Queue, Event
import argparse
import importlib.util
import sys

from registry import NODE_REGISTRY

def get_free_port():
    """Быстрый поиск и освобождение порта ОС"""
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.bind(('', 0))
        return s.getsockname()[1]


def run_node_process(node_data, status_queue, stop_event):
    """Изолированный запуск ноды в отдельном процессе"""
    import importlib
    from registry import NODE_REGISTRY

    signal.signal(signal.SIGINT, signal.SIG_IGN)

    node_type = node_data["type"]
    node_id = node_data["id"]

    # 1. Если дочерний процесс на Windows не знает об этой ноде, подгружаем её из папки
    if node_type not in NODE_REGISTRY:
        try:
            module_name = f"downloaded_nodes.{node_type}"
            module = importlib.import_module(module_name)
            NODE_REGISTRY[node_type] = getattr(module, node_type)
        except Exception as e:
            status_queue.put({"status": "error", "error": f"Сбой импорта {node_type} в новом процессе: {e}"})
            return

    # 2. Создаем ноду прямо внутри дочернего процесса (никакого Pickle!)
    try:
        NodeClass = NODE_REGISTRY[node_type]
        my_node = NodeClass(name=node_id)

        # Настраиваем провода (порты)
        for topic, port in node_data.get("publishers", {}).items():
            my_node.create_publisher(topic, port)
        for topic, addr in node_data.get("subscribers", {}).items():
            my_node.create_subscriber(topic, addr)

        my_node._stop_event = stop_event

        # Запускаем логику
        opened_ports = my_node._setup_network()
        status_queue.put({"status": "ready", "ports": opened_ports})
        my_node.loop()
    except Exception as e:
        status_queue.put({"status": "error", "error": str(e)})
    finally:
        if 'my_node' in locals():
            my_node.cleanup()

class BayMaxAgent:
    def __init__(self, name="BayMax_1", port=5000, broadcast_port=5001, mode="global", master_key=None):
        self.agent_name = name
        self.zmq_port = port
        self.broadcast_port = broadcast_port
        self.mode = mode
        self.master_key = master_key

        self.secrets_dir = ".secrets"
        self.config_file = os.path.join(self.secrets_dir, "agent_config.json")
        self.whitelist_file = os.path.join(self.secrets_dir, "authorized_keys.json")

        self.processes = []
        self.global_stop_event = Event()

        self.context = zmq.Context()
        self.cmd_socket = self.context.socket(zmq.REP)

        # Ключи самого устройства (Подготовка для CurveZMQ)
        self.public_key = None
        self.secret_key = None
        self.pin_hash = None
        self.whitelist = []

        self.downloaded_nodes_dir = "downloaded_nodes"
        if not os.path.exists(self.downloaded_nodes_dir):
            os.makedirs(self.downloaded_nodes_dir)
            # Создаем пустой __init__.py, чтобы Питон считал папку модулем
            open(os.path.join(self.downloaded_nodes_dir, "__init__.py"), 'a').close()

    def handle_check_nodes(self, config):
        """Проверка наличия нужных классов нод в реестре"""
        required_nodes = config.get("required_nodes", [])
        missing_nodes = []

        for node_name in required_nodes:
            if node_name not in NODE_REGISTRY:
                missing_nodes.append(node_name)

        if missing_nodes:
            print(f"[АГЕНТ] Отсутствуют ноды: {missing_nodes}. Запрашиваю код...")
            return {"type": "missing_nodes", "nodes": missing_nodes}

        return {"type": "nodes_ok", "status": "success"}

    def handle_upload_node(self, config):
        """Динамическая загрузка и компиляция нового кода ноды"""
        node_name = config.get("node_name")
        code = config.get("code")

        if not node_name or not code:
            return {"type": "error", "message": "Неверный формат загрузки"}

        print(f"[АГЕНТ] Скачиваю код для ноды: {node_name}...")

        # 1. Сохраняем код в файл
        filepath = os.path.join(self.downloaded_nodes_dir, f"{node_name}.py")
        with open(filepath, "w", encoding="utf-8") as f:
            f.write(code)

        # 2. Магия: Динамически импортируем этот файл в работающую программу
        try:
            module_name = f"downloaded_nodes.{node_name}"
            spec = importlib.util.spec_from_file_location(module_name, filepath)
            module = importlib.util.module_from_spec(spec)

            # Добавляем папку в sys.path на случай, если там есть зависимости
            if self.downloaded_nodes_dir not in sys.path:
                sys.path.append(self.downloaded_nodes_dir)

            spec.loader.exec_module(module)

            # 3. Ищем класс внутри модуля и добавляем в реестр
            if hasattr(module, node_name):
                node_class = getattr(module, node_name)
                NODE_REGISTRY[node_name] = node_class
                print(f"[АГЕНТ] Нода {node_name} успешно скомпилирована и добавлена в реестр!")
                return {"type": "upload_success", "status": "success"}
            else:
                return {"type": "error", "message": f"В файле не найден класс {node_name}"}

        except Exception as e:
            print(f"[АГЕНТ ОШИБКА КОМПИЛЯЦИИ] {e}")
            return {"type": "error", "message": str(e)}

    def setup_security(self):
        """Инициализация при первом запуске (Ключи и PIN)"""
        # 1. Загрузка или создание PIN-кода и ключей
        if not os.path.exists(self.secrets_dir):
            os.makedirs(self.secrets_dir)
            print(f"Создана директория для секретных данных: {self.secrets_dir}/")

        if not os.path.exists(self.config_file):
            print("\n=== ПЕРВЫЙ ЗАПУСК BAYMAX ===")
            pin = input("Придумайте PIN-код для привязки: ").strip()
            self.pin_hash = hashlib.sha256(pin.encode('utf-8')).hexdigest()

            # Генерируем пару ключей для шифрования (ZMQ Curve)
            public_key, secret_key = zmq.curve_keypair()
            self.public_key = public_key.decode('utf-8')
            self.secret_key = secret_key.decode('utf-8')

            with open(self.config_file, 'w') as f:
                json.dump({"pin_hash": self.pin_hash, "pub_key": self.public_key, "sec_key": self.secret_key}, f)
            print("Ключи сгенерированы! Пароль сохранен.\n")
        else:
            with open(self.config_file, 'r') as f:
                cfg = json.load(f)
                self.pin_hash = cfg["pin_hash"]
                self.public_key = cfg["pub_key"]
                self.secret_key = cfg["sec_key"]

        # 2. Загрузка белого списка устройств
        if os.path.exists(self.whitelist_file):
            with open(self.whitelist_file, 'r') as f:
                self.whitelist = json.load(f)
        else:
            self.whitelist = []

        if self.master_key and (self.master_key not in self.whitelist):
            self.whitelist.append(self.master_key)
            print("[БЕЗОПАСНОСТЬ] Master-ключ приложения C# добавлен в доверенные!")

    def beacon_loop(self):
        """UDP Маяк (работает в отдельном потоке)"""
        udp_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        udp_socket.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)

        beacon_data = {
            "type": "beacon",
            "agent_name": self.agent_name,
            "zmq_port": self.zmq_port,
            "public_key": self.public_key  # Раздаем открытый замок всем
        }
        payload = json.dumps(beacon_data).encode('utf-8')

        while True:
            try:
                udp_socket.sendto(payload, ('<broadcast>' if self.mode == "global" else "127.0.0.1", self.broadcast_port))
            except Exception:
                pass
            time.sleep(0.5)

    def stop_current_project(self):
        """Мягкая остановка всех запущенных нод"""
        if self.processes:
            print("[АГЕНТ] Останавливаю текущие процессы...")
            self.global_stop_event.set()
            for p in self.processes:
                p.join(timeout=3.0)
                if p.is_alive(): p.terminate()
            self.processes.clear()

    # --- ОБРАБОТЧИКИ КОМАНД (ROUTING) ---

    def handle_pair_request(self, config):
        """Этап 2: Сопряжение нового устройства"""
        received_hash = config.get("pin_hash", "")
        client_key = config.get("client_public_key")

        if received_hash == self.pin_hash and client_key:
            if client_key not in self.whitelist:
                self.whitelist.append(client_key)
                with open(self.whitelist_file, 'w') as f:
                    json.dump(self.whitelist, f)
            return {"type": "pair_response", "status": "success", "message": "Устройство авторизовано"}
        else:
            # print(received_hash, self.pin_hash)
            return {"type": "error_response", "error_code": 403, "message": "Неверный PIN-код"}

    def handle_status_request(self, config):
        """Этап 3: Пульс"""
        return {
            "type": "status_response",
            "status": "online",
            "is_running": len(self.processes) > 0
        }

    def handle_discover_request(self, config):
        """Этап 3: Поиск портов"""
        count = config.get("count", 1)
        return {"type": "discover_response", "status": "success", "ports": [get_free_port() for _ in range(count)]}

    def handle_stop_request(self, config):
        """Этап 3: Остановка"""
        self.stop_current_project()
        return {"type": "stop_response", "status": "success", "message": "Проект остановлен"}

    def handle_deploy_request(self, config):
        """Этап 3: Запуск проекта"""
        self.stop_current_project()
        self.global_stop_event = Event()

        print("\n[АГЕНТ] Начинаю развертывание проекта...")
        for node_data in config.get("nodes", []):
            node_id = node_data["id"]

            # Мы передаем чистый словарь node_data, который Питон легко проглотит
            q = Queue()
            p = Process(target=run_node_process, args=(node_data, q, self.global_stop_event))
            self.processes.append(p)
            p.start()

            res = q.get()
            if res["status"] == "error":
                print(f"[ОШИБКА] Узел {node_id} не запущен: {res['error']}")

        print("[АГЕНТ] Проект успешно запущен!")
        return {"type": "deploy_response", "status": "success", "message": "Проект запущен"}

    # --- ГЛАВНЫЙ ЦИКЛ ---

    def start(self):
        self.setup_security()
        threading.Thread(target=self.beacon_loop, daemon=True).start()

        self.cmd_socket.setsockopt(zmq.CURVE_SERVER, True)
        self.cmd_socket.setsockopt_string(zmq.CURVE_SECRETKEY, self.secret_key)

        if self.mode == "local":
            self.cmd_socket.bind(f"tcp://127.0.0.1:{self.zmq_port}")
            print(f"\n[АГЕНТ {self.agent_name}] Запущен в ЛОКАЛЬНОМ режиме. Маяк: {self.broadcast_port}")
        else:
            self.cmd_socket.bind(f"tcp://*:{self.zmq_port}")
            print(f"\n[АГЕНТ {self.agent_name}] Запущен в СЕТЕВОМ режиме. Маяк: {self.broadcast_port}")


        print(f"[АГЕНТ] Жду команды ZMQ на порту {self.zmq_port}...")

        try:
            while True:
                try:
                    incoming = self.cmd_socket.recv_string(flags=zmq.NOBLOCK, encoding='utf-8')
                except zmq.Again:
                    time.sleep(0.1)
                    continue

                # Парсим JSON
                try:
                    config = json.loads(incoming)
                    cmd_type = config.get("type")
                except json.JSONDecodeError:
                    self.cmd_socket.send_string(json.dumps({"type": "error", "message": "Invalid JSON"}),
                                                encoding='utf-8')
                    continue

                print(f"[АГЕНТ] Получена команда: {cmd_type}")

                # 1. ОБРАБОТКА АВТОРИЗАЦИИ (Pairing)
                if cmd_type == "pair_request":
                    response = self.handle_pair_request(config)
                    self.cmd_socket.send_string(json.dumps(response), encoding='utf-8')
                    continue

                # 2. ПРОВЕРКА БЕЛОГО СПИСКА ДЛЯ ОСТАЛЬНЫХ КОМАНД
                client_key = config.get("client_public_key")

                if not client_key in self.whitelist:
                    print(f"[ОТКАЗ] Неизвестное устройство пытается выполнить: {cmd_type}")
                    err = {"type": "error_response", "error_code": 401, "message": "Устройство не авторизовано"}
                    self.cmd_socket.send_string(json.dumps(err), encoding='utf-8')
                    continue

                # 3. МАРШРУТИЗАЦИЯ АВТОРИЗОВАННЫХ КОМАНД
                if cmd_type == "status_request":
                    response = self.handle_status_request(config)
                elif cmd_type == "discover_request":
                    response = self.handle_discover_request(config)
                elif cmd_type == "deploy_request":
                    response = self.handle_deploy_request(config)
                elif cmd_type == "stop_request":
                    response = self.handle_stop_request(config)
                elif cmd_type == "check_nodes":
                    response = self.handle_check_nodes(config)
                elif cmd_type == "upload_node":
                    response = self.handle_upload_node(config)
                else:
                    response = {"type": "error_response", "message": f"Неизвестная команда {cmd_type}"}

                self.cmd_socket.send_string(json.dumps(response), encoding='utf-8')

        except KeyboardInterrupt:
            print("\n[АГЕНТ] Выключение сервера...")
            self.stop_current_project()
            print("[АГЕНТ] Завершено корректно.")


if __name__ == "__main__":


    parser = argparse.ArgumentParser(description="Локальный сервер (Агент) для платформы BayMax")

    parser.add_argument("--name", type=str, default="BayMax",
                        help="Уникальное имя устройства (по умолчанию: BayMax)")

    parser.add_argument("--port", type=int, default=5000,
                        help="Основной ZMQ порт для управления (по умолчанию: 5000)")

    parser.add_argument("--broadcast-port", type=int, default=5001,
                        help="UDP порт для вещания маяка (по умолчанию: 5001)")

    parser.add_argument("--mode", type=str, default="global",
                        help="Режим работы")

    parser.add_argument("--master-key", type=str, default=None,
                        help="Публичный ключ хоста для авто-авторизации")

    args = parser.parse_args()

    agent = BayMaxAgent(name=args.name, port=args.port, broadcast_port=args.broadcast_port, mode=args.mode, master_key=args.master_key)
    agent.start()