using Autodesk.Revit.UI;

namespace Reinforcement
{
    public class App_Panel_5_2_EL_utilit
    {
        // 7. panelOV временная ничто так не временно как вечность
        public static void EL_utilit(RibbonPanel panel, string tabName)
        {
            App_Helper_Button.CreateButton("Свет", "Расстановка\n светильников", "Reinforcement.EL_panel_Light_without_boxes", Properties.Resources.EL_svetilnic,
                 "Позволяет заменить кубики КУ1301 на светильники и рубильники",
                 "Для работы плагина нужно иметь в проекте кубики",
                panel);

        }
    }
}