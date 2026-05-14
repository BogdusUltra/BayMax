from baymax import RobotNode
import time

class FilterNode(RobotNode):
    def __init__(self, name):
        super().__init__(name)
        self.create_subscriber("data_in", data_type="Number")
        self.create_publisher("data_out", data_type="Number")

        # Описываем, что хотим видеть в UI
        self.create_parameter("Threshold", param_type="number", default_value="50")
        self.create_parameter("Invert", param_type="bool", default_value="False")

    def loop(self):
        while self.is_running():
            data = self.receive("data_in")
            if data:
                res = float(data)
                # ВОТ ЗДЕСЬ мы используем параметры, которые пользователь ввел в Архитекторе!
                threshold = float(self.parameters.get("Threshold", 50))
                invert = self.parameters.get("Invert", False)

                res *= threshold

                self.publish("data_out", res)

                # ... какая-то логика фильтрации ...

            time.sleep(0.1)