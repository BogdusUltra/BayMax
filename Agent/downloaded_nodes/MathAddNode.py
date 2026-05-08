from baymax import RobotNode
import time


class MathAddNode(RobotNode):
    def __init__(self, name):
        super().__init__(name)
        # Эти строчки парсер увидит и создаст пины в C#!
        self.create_subscriber("topic_in_A", "")
        self.create_subscriber("topic_in_B", "")
        self.create_publisher("topic_out_Result", 0)

    def loop(self):
        while self.is_running():
            a = self.receive("topic_in_A")
            b = self.receive("topic_in_B")

            if a is not None or b is not None:
                print(f"[MathNode] Получил данные! A: {a}, B: {b}")

            if a is not None and b is not None:
                res = float(a) + float(b)
                print(f"[MathNode] Считаю: {res}")
                self.publish("topic_out_Result", str(res))
            time.sleep(0.01)