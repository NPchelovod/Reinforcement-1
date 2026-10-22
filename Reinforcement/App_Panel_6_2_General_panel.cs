using Autodesk.Revit.UI;
using System.Drawing;
using System.Reflection;
using System.Windows.Media;


namespace Reinforcement
{
    public class App_Panel_6_2_General_panel
    {
        public static void AddSplitButton(RibbonPanel ribbonPanel, string name)
        {
            //var data = new SplitButtonData(name, name);
            FillPullDown(ribbonPanel);//, data);
        }


        private static readonly string assemblyPath = Assembly.GetExecutingAssembly().Location;
        //private static RibbonPanel targetPanel;
        private static void FillPullDown(RibbonPanel ribbonPanel)
        {
            Image OV = Properties.Resources.Elevation;
            Image OV1 = Properties.Resources.ES_SelectWithFilter;

            // ===== 1. Инструменты =====
            var item = App_Helper_Button.CreateSplitButton(
                ribbonPanel, "ES_Instruments", "Инструменты", OV);
            if (item != null)
            {
                App_Helper_Button.AddButtonToSplitButton(
                    item, "Высотные отм труб", assemblyPath,
                    "Reinforcement.HighOtm",
                    "Расстановка высотных отметок труб по объектам",
                    OV);

                App_Helper_Button.AddButtonToSplitButton(
                    item, "Ещё команда", assemblyPath,
                    "Reinforcement.OtherCommand",
                    "Описание",
                    OV1);
            }

            // ===== 2. Память вида =====
            var item1 = App_Helper_Button.CreateSplitButton(
                ribbonPanel, "ES_MemoryViewPulldown", "Память вида", OV1);
            if (item1 != null)
            {
                App_Helper_Button.AddButtonToSplitButton(item1, "СохрВид", assemblyPath,
                    "Reinforcement.MemoryViewSave", "Сохранить вид", OV1);

                App_Helper_Button.AddButtonToSplitButton(item1, "ВосстанВид", assemblyPath,
                    "Reinforcement.MemoryViewCorrect", "Восстановить вид", OV1);
            }

            // ===== 3. Память 3D границ =====
            var item2 = App_Helper_Button.CreateSplitButton(ribbonPanel,
                "ES_SectionBoxPulldown", "Память 3D границ", OV1);
            if (item2 != null)
            {
                App_Helper_Button.AddButtonToSplitButton(item2, "Сохр3DГран", assemblyPath,
                    "Reinforcement.SectionBoxSave", "Сохранить границы 3D вида", OV1);

                App_Helper_Button.AddButtonToSplitButton(item2, "Вост3DГран", assemblyPath,
                    "Reinforcement.SectionBoxCorrect", "Восстановить границы 3D вида", OV1);
            }

            // ===== 4. Растянуть 3D границы =====
            var item3 = App_Helper_Button.CreateSplitButton(ribbonPanel,
                "ES_SectionBoxFitPulldown", "Растянуть 3D границы", OV1);
            if (item3 != null)
            {
                App_Helper_Button.AddButtonToSplitButton(item3, "3Dгор", assemblyPath,
                    "Reinforcement.SectionBoxFitHorizontal",
                    "Растянуть границы 3D горизонтально", OV1);

                App_Helper_Button.AddButtonToSplitButton(item3, "3Dверт", assemblyPath,
                    "Reinforcement.SectionBoxFitVertical",
                    "Растянуть границы 3D вертикально", OV1);
            }

            // ===== 4. Растянуть 3D границы =====
            var item4 = App_Helper_Button.CreateSplitButton(ribbonPanel,
                "ES_SectionBoxPosition", "Позиционирование По", OV1);
            if (item4 != null)
            {
                App_Helper_Button.AddButtonToSplitButton(item4, "БуферВида", assemblyPath,
                    "Reinforcement.ViewPositionCopy",
                    "Сохраняет 3Д границы и взгляд на 3Д вид", OV1);

                App_Helper_Button.AddButtonToSplitButton(item4, "Позиционировать", assemblyPath,
                    "Reinforcement.ViewPositionPaste",
                    "На другом виде можно восстановить буферизованный фзгляд", OV1);
            }

        }
        public static void Gen_utilit(RibbonPanel panel, string tabName)
        {
            //App_Helper_Button.CreateButton("Расчет кладки", "Армирование\n кладки ", "Reinforcement.CalculateReinforcementArchitectureWallsCommand", Properties.Resources.rashet_walls2,
            //     "Позволяет создать отчет армирования кладки",
            //     "Для работы плагина нужно заполнить форму",
            //    panel);
            //var data = new SplitButtonData(tabName, tabName);
            

        }
    }
}
