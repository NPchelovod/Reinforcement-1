using System;
using System.Collections.Generic;
using System.Linq;


namespace Reinforcement
{
    public enum EPanelSelf
    {
        KR,
        AR,
        OV,
        VK,
        EL,
        General,
        Test,
        Admin

    }
    public static class App_Helper_Panels
    {
        

        

        public static  List<EPanels> KRpanel { get; set; } = new List<EPanels>()
        {
            EPanels.Конфигурация,
            EPanels.СПДС,
            EPanels.СхематичноеАрмирование,
            EPanels.ДетальноеАрмирование,
            EPanels.Оформление,
            EPanels.Выбор,
            EPanels.САПР,
            EPanels.КРвставки,
            EPanels.CopyКубики,
            EPanels.ИмпортЭкспорт,
            EPanels.Опции,
            EPanels.Сюрприз,
        };
        public static  List<EPanels> ARpanel { get; set; } = new List<EPanels>()
        {
            EPanels.Конфигурация,
            EPanels.СПДС,
            EPanels.Выбор,
            EPanels.Оформление,
            EPanels.АРпанель,
            EPanels.Опции,
            EPanels.Сюрприз,
        };
        public static  List<EPanels> OVpanel { get; set; } = new List<EPanels>()
        {
            EPanels.Конфигурация,
            EPanels.СПДС,
            EPanels.ОВпанель, // Обратите внимание, что в словаре DPanels это значение записано как "ОВ панель"
            EPanels.Опции,
            EPanels.Сюрприз,
        };
        public static  List<EPanels> VKpanel { get; set; } = new List<EPanels>()
        {
            EPanels.Конфигурация,
            EPanels.СПДС,
            EPanels.Опции,
            EPanels.Сюрприз,
        };
        public static  List<EPanels> ELpanel { get; set; } = new List<EPanels>()
        {
            EPanels.Конфигурация,
            EPanels.СПДС,
            EPanels.ЭЛпанель,
            EPanels.ИмпортЭкспорт,
            EPanels.Опции,
            EPanels.Сюрприз,
        };
        public static List<EPanels> GenPanel { get; set; } = new List<EPanels>()
        {
            EPanels.Конфигурация,
            EPanels.ОбщаяПанель,
            EPanels.Опции,
        };
        public static List<EPanels> TestPanel { get; set; } = new List<EPanels>()
        {
            EPanels.Конфигурация,
             EPanels.ОВплит,
            EPanels.Опции,
        };

        public static List<EPanels> AdminPanel { get; set; } = new List<EPanels>()
        {
            EPanels.Конфигурация,
             EPanels.Разработчик,
            EPanels.Опции,
        };
        public static List<EPanels> Defaultpanel { get; set; } = new List<EPanels>()
        {
            EPanels.Конфигурация,
            EPanels.Опции,
        };


        //все панели в порядке пребывания
        public static List<EPanelSelf> LEPanelSelf { get; set; } = Enum.GetValues(typeof(EPanelSelf))
            .Cast<EPanelSelf>()
            .ToList();


        public static List<EPanels> GetListEPanel(EPanelSelf ePanelSelf)
        {

            switch (ePanelSelf)
            {
                case (EPanelSelf.KR):
                    return KRpanel;

                case (EPanelSelf.AR):
                    return ARpanel;
                case (EPanelSelf.OV):
                    return OVpanel;
                case (EPanelSelf.VK):
                    return OVpanel;
                case (EPanelSelf.EL):
                    return ELpanel;
                case (EPanelSelf.General):
                    return GenPanel;
                case (EPanelSelf.Test):
                    return TestPanel;
                case (EPanelSelf.Admin):
                    return AdminPanel;

            }
            return Defaultpanel;
        }

        public static void CreatePanelConfiguration(EPanelSelf ePanelSelf)
        {
            List<EPanels>ePanels = GetListEPanel(ePanelSelf);
            foreach (var panel in PanelVisibility.Panels)
            {
                if (ePanels.Contains(panel.Key))
                {
                    if (panel.Value != null)
                    {
                        panel.Value.Visible = true;
                    }
                }
                else
                {
                    if (panel.Value != null)
                    {
                        panel.Value.Visible = false;
                    }
                }
            }
        }


    }

}
