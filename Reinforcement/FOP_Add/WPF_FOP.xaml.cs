using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using static System.Net.Mime.MediaTypeNames;
using static Autodesk.Revit.DB.SpecTypeId;

namespace Reinforcement
{
    /// <summary>
    /// Логика взаимодействия для WPF_FOP.xaml
    /// </summary>
    public partial class WPF_FOP : Window
    {
        private ExternalCommandData  externalCommandData;
        public WPF_FOP(ExternalCommandData commandData)
        {
            InitializeComponent();
            InitialData();
            externalCommandData = commandData;
        }


        public static string FOPWriters = "ЕС_Автор,ЕС_Посл Автор";

        private void InitialData()
        {
            namesFop.Text = FOPWriters;
            chkIdentity.IsChecked = _identity;
             chkGeneral.IsChecked = _igeneral;
            chkData.IsChecked = _idata;
            chkText.IsChecked = _itext;
        }


        public static bool _identity = true;
        public static bool _igeneral = false;
        public static bool _idata=false;
        public static bool _itext = false;
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Сохраняем значения
            // AddinSettings.FillAuthor = chkFillAuthor.IsChecked == true;

            // При необходимости можно вызвать сохранение в файл/реестр
            //AddinSettings.Save();
            FOPWriters = namesFop.Text;

            List<string> result = FOPWriters
            .Split(',')                 // разбиваем по запятой
            .Select(s => s.Trim())      // убираем пробелы по краям
            .Where(s => !string.IsNullOrEmpty(s)) // отбрасываем пустые строки (если были двойные запятые)
            .ToList();

            _identity = chkIdentity.IsChecked == true;
            _igeneral = chkGeneral.IsChecked == true;
            _idata = chkData.IsChecked == true;
            _itext = chkText.IsChecked == true;
            BuiltInParameterGroup builtInParameterGroup = BuiltInParameterGroup.PG_IDENTITY_DATA;

            if (_igeneral)
            {
                builtInParameterGroup = BuiltInParameterGroup.PG_GENERAL;
            }
            else if (_idata)
            {
                builtInParameterGroup = BuiltInParameterGroup.PG_DATA;
            }
            else if(_itext)
            {
                builtInParameterGroup = BuiltInParameterGroup.PG_TEXT;
            }
            if (result.Count > 0)
            {
                foreach (string s in result)
                {
                    FopAdd.RegParameter(externalCommandData, s, builtInParameterGroup);
                }
            }


            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
