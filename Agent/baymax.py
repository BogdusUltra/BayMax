import zmq
import time
import json

class RobotNode:
    def __init__(self, name):
        self.name = name
        self._pub_config = {}
        self._sub_config = {}
        self.context = None
        self.publishers = {}
        self.subscribers = {}
        self._stop_event = None
        self.parameters = {}


    def is_running(self):
        if self._stop_event is not None:
            return not self._stop_event.is_set()
        return True

    def create_publisher(self, topic_name, data_type="Any", port=0):
        self._pub_config[topic_name] = {"port": port, "data_type": data_type}

    def create_subscriber(self, topic_name, data_type="Any", address=None):
        self._sub_config[topic_name] = {"address": address, "data_type": data_type}

    def set_publisher_port(self, topic_name, port):
        if topic_name in self._pub_config:
            self._pub_config[topic_name]["port"] = port

    def set_subscriber_address(self, topic_name, address):
        if topic_name in self._sub_config:
            self._sub_config[topic_name]["address"] = address

    def create_parameter(self, name, param_type="text", default_value=""):
        self.parameters[name] = default_value

    def _setup_network(self):
        self.context = zmq.Context()
        assigned_ports = {}

        # 1. Открываем топики вещания (PUB)
        for topic_name, config in self._pub_config.items():
            port = config["port"]

            sock = self.context.socket(zmq.PUB)
            if port == 0:
                actual_port = sock.bind_to_random_port("tcp://*")
            else:
                sock.bind(f"tcp://*:{port}")
                actual_port = port

            self.publishers[topic_name] = sock
            assigned_ports[topic_name] = actual_port

        # 2. Подключаемся к чужим топикам (SUB)

        for topic_name, config in self._sub_config.items():
            address = config.get("address")
            if not address:
                continue

            sock = self.context.socket(zmq.SUB)
            sock.connect(address)
            sock.setsockopt_string(zmq.SUBSCRIBE, "")
            self.subscribers[topic_name] = sock

        return assigned_ports

    def publish(self, topic_name, data):
        if topic_name in self.publishers:
            if isinstance(data, (dict, list)):
                string_data = json.dumps(data, ensure_ascii=False)
            else:
                string_data = str(data)

            self.publishers[topic_name].send_string(string_data, encoding='utf-8')

    def receive(self, topic_name):
        if topic_name in self.subscribers:
            try:
                return self.subscribers[topic_name].recv_string(flags=zmq.NOBLOCK, encoding='utf-8')
            except zmq.Again:
                return None
        return None

    def loop(self):
        raise NotImplementedError(f"[{self.name}] Метод loop() должен быть переопределен.")

    def cleanup(self):
        print(f"[{self.name}] Начинаю освобождение ресурсов...")
        for sock in self.publishers.values(): sock.close()
        for sock in self.subscribers.values(): sock.close()
        if self.context:
            self.context.term()
        print(f"[{self.name}] Работа завершена, ресурсы освобождены.")

# --- ПОЛЬЗОВАТЕЛЬСКИЕ НОДЫ ---
class TemperatureSensorNode(RobotNode):
    def loop(self):
        temp = 20
        while self.is_running():
            self.publish("temp_data", f"Температура: {temp} °C")
            temp += 1
            time.sleep(1) # Задержка в 1 секунду, чтобы не спамить
        print(f"[{self.name}] Работа цикла завершена. Перехожу к закрытию...")

class DisplayNode(RobotNode):
    def loop(self):
        while self.is_running():
            data = self.receive("incoming_temp")
            if data:
                print(f"[{self.name}] Получил <- {data}")
            time.sleep(0.1)
        print(f"[{self.name}] Работа цикла завершена. Перехожу к закрытию...")