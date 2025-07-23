using Autodesk.Revit.UI;

namespace Reinforcement
{
    public class App_Panel_5_2_EL_utilit
    {
        // 7. panelOV временная ничто так не временно как вечность
        public static void EL_utilit(RibbonPanel panel, string tabName)
        {
            App_Helper_Button.CreateButton("Свет", "Расстановка\n светильников", "Reinforcement.CopyElectricFromTask", Properties.Resources.EL_svetilnic,
                 "Позволяет заменить кубики на светильники и рубильники",
                 "Для работы плагина нужно иметь в проекте кубики",
                panel);

        }
    }
}