from baymax import RobotNode
import time


class MathAddNode(RobotNode):
    def __init__(self, name):
        super().__init__(name)
        self.create_subscriber("topic_in_A", data_type="Number")
        self.create_subscriber("topic_in_B", data_type="Number")

        self.create_publisher("topic_out_Result", data_type="Number")

        self.last_a = None
        self.last_b = None

    def loop(self):
        while self.is_running():
            a = self.receive("topic_in_A")
            b = self.receive("topic_in_B")

            if a is not None:
                self.last_a = float(a)

            if b is not None:
                self.last_b = float(b)

            if self.last_a is not None and self.last_b is not None:
                res = self.last_a + self.last_b
                self.publish("topic_out_Result", res)

            time.sleep(0.01)