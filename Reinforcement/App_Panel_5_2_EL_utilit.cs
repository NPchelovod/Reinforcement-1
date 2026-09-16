using System.Linq;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    public class App_Panel_5_2_EL_utilit
    {
        // 7. panelOV временная ничто так не временно как вечность
        public static void EL_utilit(RibbonPanel panel, string tabName)
        {
            App_Helper_Button.CreateButton("Свет", "Расстановка\n светильников", "Reinforcement.EL_panel_Light_without_boxes", Properties.Resources.EL_svetilnic,
                 "Позволяет заменить кубики КУ1301 на светильники и патроны",
                 "Для работы плагина нужно иметь в проекте кубики",
                panel);

            App_Helper_Button.CreateButton("Гусак", "Гусак", "Reinforcement.EL_GusakRoof", Properties.Resources.GRebar,
                "Размещение Гусака", $"Имя семейства должно быть {EL_GusakRoof.list_Name.FirstOrDefault()}", panel);
            App_Helper_Button.CreateButton("Закладная\nплиты", "Закладная\nплиты", "Reinforcement.ZacladInSlab", Properties.Resources.ES_RebarFromSide,
                "Размещение Закладная\nплиты", $"Имя семейства должно быть {ZacladInSlab.list_Name.FirstOrDefault()}", panel);
            App_Helper_Button.CreateButton("Закладная\nввода", "Закладная\nввода", "Reinforcement.EL_Vvod", Properties.Resources.Arrow_of_view,
                "Размещение Закладная ввода", $"Имя семейства должно быть {EL_Vvod.list_Name.FirstOrDefault()}", panel);
            App_Helper_Button.CreateButton("Кубик\nплита круг", "Кубик\nплита круг", "Reinforcement.CubicCircle", Properties.Resources.ES_RebarInFront,
                "Размещение Кубик_Перекрытие_Круг", $"Имя семейства должно быть {CubicCircle.list_Name.FirstOrDefault()}", panel);
            App_Helper_Button.CreateButton("Кубик\nплита прям", "Кубик\nплита прям", "Reinforcement.CubicSlabRectangle", Properties.Resources.HomutDistrib,
                "Размещение Кубик_Перекрытие_Прямоугольный", $"Имя семейства должно быть {CubicSlabRectangle.list_Name.FirstOrDefault()}", panel);
            App_Helper_Button.CreateButton("Кубик\nстена круг", "Кубик\nстена круг", "Reinforcement.CubicWallCircle", Properties.Resources.ES_RebarInFront,
                "Размещение Кубик_Стена_Круг", $"Имя семейства должно быть {CubicWallCircle.list_Name.FirstOrDefault()}", panel);
            App_Helper_Button.CreateButton("Кубик\nстена прям", "Кубик\nстена прям", "Reinforcement.CubicWallRectangle", Properties.Resources.HomutDistrib,
                "Размещение Кубик_Стена_Прямоугольный", $"Имя семейства должно быть {CubicWallRectangle.list_Name.FirstOrDefault()}", panel);

        }
    }
}