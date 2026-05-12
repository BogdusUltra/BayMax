import inspect
import baymax
import multiprocessing
import importlib.util
import os

def build_node_registry():
    registry = {}
    is_main = multiprocessing.current_process().name == "MainProcess"

    for name, obj in inspect.getmembers(baymax):
        if inspect.isclass(obj) and issubclass(obj, baymax.RobotNode):
            if name != "RobotNode":
                registry[name] = obj

                if is_main:
                    print(f"[РЕЕСТР] Успешно зарегистрирована нода: {name}")

    custom_dir = "downloaded_nodes"
    if os.path.exists(custom_dir):
        for filename in os.listdir(custom_dir):
            if filename.endswith(".py") and filename != "__init__.py":
                module_name = filename[:-3]
                filepath = os.path.join(custom_dir, filename)

                try:
                    spec = importlib.util.spec_from_file_location(f"downloaded_nodes.{module_name}", filepath)
                    module = importlib.util.module_from_spec(spec)
                    spec.loader.exec_module(module)

                    for name, obj in inspect.getmembers(module):
                        if inspect.isclass(obj) and issubclass(obj, baymax.RobotNode):
                            if name != "RobotNode":
                                registry[name] = obj
                                if is_main:
                                    print(f"[РЕЕСТР] Кастомная нода восстановлена: {name}")
                except Exception as e:
                    if is_main:
                        print(f"[РЕЕСТР ОШИБКА] Не удалось загрузить {filename}: {e}")

    return registry



NODE_REGISTRY = build_node_registry()