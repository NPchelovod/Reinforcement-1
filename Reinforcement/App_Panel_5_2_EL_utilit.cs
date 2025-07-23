using Autodesk.Revit.UI;

namespace Reinforcement
{
    public class App_Panel_5_2_EL_utilit
    {
        // 7. panelOV временная ничто так не временно как вечность
        public static void EL_utilit(RibbonPanel panel, string tabName)
        {
            App_Helper_Button.CreateButton("Свет", "Расстановка\n светильников и рубильников ", "Reinforcement.CopyElectricFromTask", Properties.Resources.rashet_walls2,
                 "Позволяет заменить кубики на светильники и рубильники",
                 "Для работы плагина нужно просто работать",
                panel);

        }
    }
}