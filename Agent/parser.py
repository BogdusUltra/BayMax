import ast
import os
import json

def parse_nodes(folder_path):
    schema = []
    for filename in os.listdir(folder_path):
        if not filename.endswith(".py") or filename == "baymax.py" or filename == "registry.py" or filename == "agent.py":
            continue

        path = os.path.join(folder_path, filename)
        with open(path, "r", encoding="utf-8") as f:
            tree = ast.parse(f.read())

        for node in ast.walk(tree):
            if isinstance(node, ast.ClassDef):
                node_info = {
                    "name": node.name,
                    "title": node.name,  # Можно достать из переменной класса, если она есть
                    "category": "Custom",
                    "inputs": [],
                    "outputs": [],
                    "source_code": open(path, "r", encoding="utf-8").read()
                }

                for item in ast.walk(node):
                    if isinstance(item, ast.Call) and isinstance(item.func, ast.Attribute):
                        if item.func.attr == "create_publisher" or item.func.attr == "create_subscriber":
                            topic_name = item.args[0].value
                            data_type = "Any"

                            for kw in item.keywords:
                                if kw.arg == "data_type" and hasattr(kw.value, 'value'):
                                    data_type = kw.value.value

                            pin_info = {"name": topic_name, "data_type": data_type}

                            if item.func.attr == "create_publisher":
                                node_info["outputs"].append(pin_info)
                            else:
                                node_info["inputs"].append(pin_info)

                schema.append(node_info)

    with open("nodes_schema.json", "w", encoding="utf-8") as f:
        json.dump(schema, f, indent=4, ensure_ascii=False)


if __name__ == "__main__":
    parse_nodes("./CustomNodes")