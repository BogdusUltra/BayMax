from baymax import RobotNode
import time


class MathAddNode(RobotNode):
    def __init__(self, name):
        super().__init__(name)
        self.create_subscriber("topic_in_A", "")
        self.create_subscriber("topic_in_B", "")
        self.create_publisher("topic_out_Result", 0)

        # # ДОБАВЛЯЕМ ПАМЯТЬ ДЛЯ ПИНОВ
        self.last_a = None
        self.last_b = None

    def loop(self):
        while self.is_running():
            # Забираем новые данные из сети
            a = self.receive("topic_in_A")
            b = self.receive("topic_in_B")

            # changed = False

            # Если пришли новые данные, обновляем память
            if a is not None:
                self.last_a = float(a)

            if b is not None:
                self.last_b = float(b)

            # Считаем и отправляем ТОЛЬКО если что-то поменялось и обе цифры уже известны
            if self.last_a is not None and self.last_b is not None:
                res = self.last_a + self.last_b
                # print(f"[MathNode] Считаю: {a} + {b} = {res}")
                self.publish("topic_out_Result", str(res))

            # print("hello")
            time.sleep(0.01)