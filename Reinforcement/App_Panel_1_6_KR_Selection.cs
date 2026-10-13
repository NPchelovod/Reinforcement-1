using Autodesk.Revit.UI;


namespace Reinforcement
{
    public class App_Panel_1_6_KR_Selection
    {
        public static void KR_Selection(RibbonPanel panelSelection, string tabName)
        {
            App_Helper_Button.CreateButton("Найти элемент", "Найти\nэлемент", "Reinforcement.SelectParentElement", Properties.Resources.ЕС_Выбор,
               "Позволяет найти семейство, даже в спецификации в спецификации",
               "A. Выбрать искомый экземпляр или ввести его вручную\n" +
               "Б. или ввести семейство искомое вручную\n" +
               "В1. Выделить нужную деталь в строчке спецификации\n" +
               "В2. Нажать на кнопку\n" +
              "В3. Перейти на любой другой вид.\nТеперь можно поменять свойства детали",
              panelSelection);

            App_Helper_Button.CreateButton("Выбор с фильтром", "Выбор\nс фильтром", "Reinforcement.CommandPickWithFilter", Properties.Resources.ES_SelectWithFilter,
     "Выбрать элементы по значению параметра - Тип элемента", "тут какая то большая подсказка должна быть я не придумал", panelSelection);
        }
    }
}
