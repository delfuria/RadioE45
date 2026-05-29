using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RadioE45.Logic
{
    public class RadioStation
    {
        public string Name { get; set; }
        public string URL { get; set; }
        public string IconPath { get; set; }

        public GradientStop LightColor { get; set; }
        public GradientStop DarkColor { get; set; }

        public RadioStation(string name, string url, string iconPath, Color lightColor, Color darkColor)
        {
            this.Name = name;
            this.URL = url;
            this.IconPath = iconPath;

            LightColor = new GradientStop(lightColor, 0);
            DarkColor = new GradientStop(darkColor, 1);
        }
        public RadioStation(string name, string url, string iconPath, string lightColor, string darkColor)
        {
            this.Name = name;
            this.URL = url;
            this.IconPath = iconPath;

            LightColor = new GradientStop(Color.FromHex(lightColor), 0);
            DarkColor = new GradientStop(Color.FromHex(darkColor), 1);
        }
    }
}
