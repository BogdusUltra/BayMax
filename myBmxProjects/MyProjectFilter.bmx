{
  "ProjectName": "MyProjectFilter",
  "SaveTime": "2026-05-14T19:59:09.4889929+03:00",
  "ProjectDevices": [
    {
      "Name": "BayMax",
      "Ip": "100.81.109.53"
    }
  ],
  "Nodes": [
    {
      "Id": "740692e0b3184a5f99044af666543685",
      "Type": "UI",
      "LogicTypeName": "\u0427\u0438\u0441\u043B\u043E (\u0412\u0432\u043E\u0434)",
      "X": 50096,
      "Y": 50182.85714285714,
      "Width": 150,
      "Height": 100,
      "SavedPinIds": [
        "033650b793364bfbb44d9e9f582d2b9b"
      ],
      "Settings": {
        "NumberValue": "14"
      }
    },
    {
      "Id": "e58de6a20b1649438cec9c5ddabb6ac4",
      "Type": "Logic",
      "LogicTypeName": "FilterNode",
      "X": 50507.42857142857,
      "Y": 50129.71428571429,
      "Width": 290.86571428571324,
      "Height": 156.2297686059111,
      "SavedPinIds": [
        "20774ecd403444a99374a461376d70e3",
        "5417fcb2e1554f238229d8f4b5a2f071"
      ],
      "Settings": {
        "Threshold": "14",
        "Invert": "True",
        "_TargetDeviceIp": "100.81.109.53"
      }
    },
    {
      "Id": "f3f9eb8c90074292a224a49b8a0a9704",
      "Type": "UI",
      "LogicTypeName": "\u0427\u0438\u0441\u043B\u043E (\u0412\u044B\u0432\u043E\u0434)",
      "X": 50798.28571428572,
      "Y": 50327.428571428565,
      "Width": 178.52666666666664,
      "Height": 101.92333333333333,
      "SavedPinIds": [
        "ad4232898c374434820d4a5c90ca6b9f"
      ],
      "Settings": {}
    },
    {
      "Id": "2f9ca802041e44f3a5867c11549274f4",
      "Type": "UI",
      "LogicTypeName": "\u0427\u0438\u0441\u043B\u043E (\u0412\u0432\u043E\u0434)",
      "X": 50256.57142857143,
      "Y": 50343.75428571429,
      "Width": 150,
      "Height": 100,
      "SavedPinIds": [
        "e076f71558c34b0b8c4e2a8815f1c6d8"
      ],
      "Settings": {
        "NumberValue": "0"
      }
    }
  ],
  "Connections": [
    {
      "StartNodeId": "e58de6a20b1649438cec9c5ddabb6ac4",
      "StartPinId": "5417fcb2e1554f238229d8f4b5a2f071",
      "EndNodeId": "f3f9eb8c90074292a224a49b8a0a9704",
      "EndPinId": "ad4232898c374434820d4a5c90ca6b9f"
    },
    {
      "StartNodeId": "740692e0b3184a5f99044af666543685",
      "StartPinId": "033650b793364bfbb44d9e9f582d2b9b",
      "EndNodeId": "e58de6a20b1649438cec9c5ddabb6ac4",
      "EndPinId": "20774ecd403444a99374a461376d70e3"
    }
  ]
}