using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace BayMax.UI.Controls
{
    public static class UIHelper
    {
        public static T FindParent<T>(this DependencyObject child) where T : DependencyObject
        {
            if (child == null) return null;

            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            while (parentObject != null && !(parentObject is T))
            {
                parentObject = VisualTreeHelper.GetParent(parentObject);
            }

            return parentObject as T;
        }
    }
}
