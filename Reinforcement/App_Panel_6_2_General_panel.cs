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
            var data = new SplitButtonData(name, name);
            FillPullDown(ribbonPanel, data);
        }


        private static readonly string assemblyPath = Assembly.GetExecutingAssembly().Location;
        //private static RibbonPanel targetPanel;
        private static void FillPullDown(RibbonPanel ribbonPanel, PulldownButtonData data)
        {
            Image OV = Properties.Resources.Elevation;
            Image OV1 = Properties.Resources.ES_SelectWithFilter;

            var item = App_Helper_Button.CreatePulldownButton(ribbonPanel,
                "ES_Instruments", "Инструменты", OV);

            var item1 = App_Helper_Button.CreatePulldownButton(ribbonPanel,
                "ES_MemoryViewPulldown", "Память вида", OV1);
            var item2 = App_Helper_Button.CreatePulldownButton(ribbonPanel,
                "ES_SectionBoxPulldown", "Память 3D границ", OV1);
            var item3 = App_Helper_Button.CreatePulldownButton(ribbonPanel,
                "ES_SectionBoxFitPulldown", "Растянуть 3D границы", OV1);

            App_Helper_Button.AddButtonToPullDownButton(item, "Высотные отм", assemblyPath, "Reinforcement.HighOtm", $"Расстановка высотных отметок по обьектам\nможете выделить *///обьекты, можете не выделять", OV);


            App_Helper_Button.AddButtonToPullDownButton(item1, "СохрВид", assemblyPath, "Reinforcement.MemoryViewSave", "Сохранить вид", OV1);
            App_Helper_Button.AddButtonToPullDownButton(item1, "ВосстанВид", assemblyPath, "Reinforcement.MemoryViewCorrect", "Восстановить вид", OV1);

            App_Helper_Button.AddButtonToPullDownButton(item2, "Сохр3DГран", assemblyPath, "Reinforcement.SectionBoxSave", "Сохранить границы 3D вида", OV1);
            App_Helper_Button.AddButtonToPullDownButton(item2, "Вост3DГран", assemblyPath, "Reinforcement.SectionBoxCorrect", "Восстановить границы 3D вида", OV1);

            App_Helper_Button.AddButtonToPullDownButton(item3, "3Dгор", assemblyPath, "Reinforcement.SectionBoxFitHorizontal", "Растянуть границы 3D горизонтально", OV1);
            App_Helper_Button.AddButtonToPullDownButton(item3, "3Dверт", assemblyPath, "Reinforcement.SectionBoxFitVertical", "Растянуть границы 3D вертикально", OV1);


            

            //var item = ribbonPanel.AddItem(data) as PulldownButton;
            //Image OV1 = Properties.Resources.ES_SelectWithFilter;
            ////Image OV2 = Properties.Resources.toska2;
            //var item2 = ribbonPanel.AddItem(data) as PulldownButton;
            //var item3 = ribbonPanel.AddItem(data) as PulldownButton;




            //App_Helper_Button.CreateButton("Высотные отм труб", "Высотные отм труб", "Reinforcement.HighOtm", Properties.Resources.Elevation, "Позволяет проставить высотные отметки", "на выделенных трубах или без выделения", ribbonPanel);

            //App_Helper_Button.AddButtonToPullDownButton(item, "СохрВид", assemblyPath, "Reinforcement.MemoryViewSave", $"Сохранить вид", OV1);
            //App_Helper_Button.AddButtonToPullDownButton(item, "ВосстанВид", assemblyPath, "Reinforcement.MemoryViewCorrect", $"Восстановить вид из записанного ранее состояния", OV1);


            //App_Helper_Button.AddButtonToPullDownButton(item2, "Сохр3DГран", assemblyPath, "Reinforcement.SectionBoxSave", $"Сохранить границы 3D вида", OV1);
            //App_Helper_Button.AddButtonToPullDownButton(item2, "Вост3DГран", assemblyPath, "Reinforcement.SectionBoxCorrect", $"Восстановить границы 3D вида", OV1);

            //App_Helper_Button.AddButtonToPullDownButton(item3, "3Dгор", assemblyPath, "Reinforcement.SectionBoxFitHorizontal", $"растянуть границы 3D вида горизонтально", OV1);
            //App_Helper_Button.AddButtonToPullDownButton(item3, "3Dверт", assemblyPath, "Reinforcement.SectionBoxFitVertical", $"растянуть границы 3D вида вертикально", OV1);





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
