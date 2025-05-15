using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Configuration.Assemblies;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
/*
namespace Reinforcement



// здесь все создатели для кнопок и тп, https://dzen.ru/a/ZU9n_-2BS2sY7lQ2  https://github.com/SergeyNefyodov/Revit-API-Blog/blob/master/RevitApplication.cs
{
    internal class App_Panel_Helper
    {

        public static void AddRadioButtonGroup(RibbonPanel ribbonPanel, string name, string linkToCommand, string name_commit = "Кнопка", int num = 1)
        {
            var data = new RadioButtonGroupData(name);

            var item = ribbonPanel.AddItem(data) as RadioButtonGroup;
            AddToggleButton(item, "ToggleButton 1", assemblyPath, linkToCommand, name_commit, num);
            //AddToggleButton(item,"ToggleButton 1", assemblyPath, "","Кнопка переключатель 1")

        }

        private void AddToggleButton(RadioButtonGroup group, string buttonName, string path, string linkToCommand, string toolTip, int i)
        {
            ToggleButtonData data = new ToggleButtonData(buttonName, buttonName, path, linkToCommand);
            var item = group.AddItem(data) as ToggleButton;
            item.ToolTip = toolTip;
            // путь жл картинки
            item.LargeImage = (ImageSource)new BitmapImage();

        }

        public static void AddPullDownButton(RibbonPanel ribbonPanel, string name)
        {
            var data = new PulldownButtonData(name, name);
            FillPullDown(ribbonPanel, data);
        }

        private void AddSplitButton(RibbonPanel ribbonPanel, string name)
        {
            var data = new SplitButtonData(name, name);
            FillPullDown(ribbonPanel, data);

        }

        private void FillpullDown(RibbonPanel ribbonPanel, PulldownButtonData data)
        {
            var item = ribbonPanel.AddItem(data)as RadioButtonGroup;

            var spisok_comand = new List<string>()
            {
                "КР", "ОВ", "ВК", "ЭЛ"
            };

            var spisok_linkToCommand = new List<string>()
            {
                "прописывешь команду кнопки"
            };

            int tek_num = -1;
            foreach(var sp in spisok_comand)
            {
                tek_num += 1;

                AddPullDownButton(item, sp, assemblyPath, spisok_linkToCommand[tek_num], "подсказка");
            }
            item.AddSeparator();
            item.LargeImage = "путь до пнг";

        }

        private void AddButtonToPullDownButton(PulldownButton button, string name, string path, string LankToCommand, string toolTip)
        {
            var data = new PushButtonData(name, name, path, LankToCommand);
            var pushButton = button.AddPushButton(data) as PushButton;
            pushButton.ToolTip = toolTip;
            pushButton.Image = "источник";
            pushButton.LargeImage = "источник";


        }









    }
}
*/
