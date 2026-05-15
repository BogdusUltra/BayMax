using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;

namespace BayMax.UI.Components
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
        public static Color GetBaseColor(PinDataType type) => type switch
        {
            PinDataType.Any => Color.FromRgb(150, 150, 150),    // Серый
            PinDataType.String => Color.FromRgb(34, 139, 34),   // Темно-зеленый
            PinDataType.Number => Color.FromRgb(0, 100, 200),   // Темно-синий
            PinDataType.Boolean => Color.FromRgb(128, 0, 128),  // Темно-фиолетовый
            PinDataType.Image => Color.FromRgb(180, 150, 0),    // Темно-желтый (горчичный)
            PinDataType.H264 => Color.FromRgb(200, 100, 0),     // Темно-оранжевый
            PinDataType.Json => Color.FromRgb(150, 0, 0),       // Темно-красный
            _ => Color.FromRgb(100, 100, 100)
        };

        public static Color GetBrightColor(PinDataType type) => type switch
        {
            PinDataType.Any => Color.FromRgb(255, 255, 255),    // Белый
            PinDataType.String => Color.FromRgb(0, 255, 0),     // Ярко-зеленый
            PinDataType.Number => Color.FromRgb(0, 191, 255),   // Ярко-синий (Голубой)
            PinDataType.Boolean => Color.FromRgb(218, 112, 214),// Ярко-фиолетовый
            PinDataType.Image => Color.FromRgb(255, 215, 0),    // Ярко-желтый
            PinDataType.H264 => Color.FromRgb(255, 140, 0),     // Ярко-оранжевый
            PinDataType.Json => Color.FromRgb(255, 50, 50),     // Ярко-красный
            _ => Color.FromRgb(200, 200, 200)
        };
    }
}
