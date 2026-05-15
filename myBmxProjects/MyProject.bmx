{
  "ProjectName": "MyProject",
  "SaveTime": "0001-01-01T00:00:00",
  "ProjectDevices": [
    {
      "Name": "BayMax",
      "Ip": "100.81.109.53"
    }
  ],
  "CanvasData": {
    "Nodes": [
      {
        "Id": "aebaa4be919945928925223a510b8121",
        "Type": "UI",
        "LogicTypeName": "\u0427\u0438\u0441\u043B\u043E (\u0412\u0432\u043E\u0434)",
        "X": 50138.28571428571,
        "Y": 50080.897142857146,
        "Width": 150,
        "Height": 100,
        "SavedPinIds": [
          "be9b4c771c394e47856f2582c7a28b0d"
        ],
        "Settings": {
          "NumberValue": "5"
        }
      },
      {
        "Id": "3cfa4435c1a64c72a5bf68e91147dee4",
        "Type": "Logic",
        "LogicTypeName": "FilterNode",
        "X": 50477.30285714286,
        "Y": 50029.66285714287,
        "Width": 351.80857142857593,
        "Height": 159.65834003448487,
        "SavedPinIds": [
          "32923191a50d48e0b7e65ecad8224b4b",
          "ab1e644674214999b370b738059803b3"
        ],
        "Settings": {
          "Threshold": "2",
          "Invert": "False",
          "_TargetDeviceIp": "100.81.109.53"
        }
      },
      {
        "Id": "146df548088442b598d48dc07db655cb",
        "Type": "UI",
        "LogicTypeName": "\u0427\u0438\u0441\u043B\u043E (\u0412\u044B\u0432\u043E\u0434)",
        "X": 50929.142857142855,
        "Y": 50060.32571428571,
        "Width": 178.52666666666664,
        "Height": 101.92333333333333,
        "SavedPinIds": [
          "2901b21fa8bf45d79b19d07e00666efb"
        ],
        "Settings": {}
      },
      {
        "Id": "b52ef10ed1bb499f91e0a0b390d197e3",
        "Type": "Logic",
        "LogicTypeName": "FilterNode",
        "X": 50508.57142857142,
        "Y": 50405.468571428566,
        "Width": 284.58000000000004,
        "Height": 155.65834003448487,
        "SavedPinIds": [
          "0f14939f38384cb1af994d7feeec8a52",
          "a7108e57d6a74375905b5ba3961ae49b"
        ],
        "Settings": {
          "Threshold": "5",
          "Invert": "False",
          "_TargetDeviceIp": "100.81.109.53"
        }
      },
      {
        "Id": "2efb6eff512341a984b7be8c9c827a74",
        "Type": "UI",
        "LogicTypeName": "\u0427\u0438\u0441\u043B\u043E (\u0412\u044B\u0432\u043E\u0434)",
        "X": 50972,
        "Y": 50336.32571428572,
        "Width": 178.52666666666664,
        "Height": 101.92333333333333,
        "SavedPinIds": [
          "ba89ef8a72a74e24b6bae89559a33c81"
        ],
        "Settings": {}
      }
    ],
    "Connections": [
      {
        "StartNodeId": "b52ef10ed1bb499f91e0a0b390d197e3",
        "StartPinId": "a7108e57d6a74375905b5ba3961ae49b",
        "EndNodeId": "2efb6eff512341a984b7be8c9c827a74",
        "EndPinId": "ba89ef8a72a74e24b6bae89559a33c81"
      },
      {
        "StartNodeId": "3cfa4435c1a64c72a5bf68e91147dee4",
        "StartPinId": "ab1e644674214999b370b738059803b3",
        "EndNodeId": "b52ef10ed1bb499f91e0a0b390d197e3",
        "EndPinId": "0f14939f38384cb1af994d7feeec8a52"
      },
      {
        "StartNodeId": "3cfa4435c1a64c72a5bf68e91147dee4",
        "StartPinId": "ab1e644674214999b370b738059803b3",
        "EndNodeId": "146df548088442b598d48dc07db655cb",
        "EndPinId": "2901b21fa8bf45d79b19d07e00666efb"
      },
      {
        "StartNodeId": "aebaa4be919945928925223a510b8121",
        "StartPinId": "be9b4c771c394e47856f2582c7a28b0d",
        "EndNodeId": "3cfa4435c1a64c72a5bf68e91147dee4",
        "EndPinId": "32923191a50d48e0b7e65ecad8224b4b"
      }
    ]
  }
}