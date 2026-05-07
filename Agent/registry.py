import inspect
import baymax
import multiprocessing

def build_node_registry():
    registry = {}

    for name, obj in inspect.getmembers(baymax):
        if inspect.isclass(obj) and issubclass(obj, baymax.RobotNode):
            if name != "RobotNode":
                registry[name] = obj

                if multiprocessing.current_process().name == "MainProcess":
                    print(f"[РЕЕСТР] Успешно зарегистрирована нода: {name}")
    return registry

NODE_REGISTRY = build_node_registry()