using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;

namespace BayMax.Nodes
{

    public enum PinDataType
    {
        Any,      // Универсальный тип (может принимать что угодно)
        String,   // Текст
        Number,   // Числа (int, float)
        Boolean,  // Да/Нет (True/False)
        Image,    // Картинка (Bitmap/Пиксели)
        H264,      // Видеопоток
        Json
    }
    public static class DataTypeColors
    {
        public static SolidColorBrush GetColor(PinDataType type)
        {
            return type switch
            {
                PinDataType.Any => new SolidColorBrush(Colors.White),
                PinDataType.String => new SolidColorBrush(Colors.Green),
                PinDataType.Number => new SolidColorBrush(Colors.Blue),
                PinDataType.Boolean => new SolidColorBrush(Colors.Purple),
                PinDataType.Image => new SolidColorBrush(Colors.Yellow),
                PinDataType.H264 => new SolidColorBrush(Colors.Orange),
                PinDataType.Json => new SolidColorBrush(Colors.Red),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
    }
}
