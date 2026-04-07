using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Microsoft.Win32;
using Spire.Xls;
using Spire.Xls.Collections;

namespace Reinforcement
{
    public partial class ExcelImportWindow : Window
    {
        public string SelectedFilePath { get; private set; }
        public string SelectedSheetName { get; private set; }
        public ViewSchedule SelectedSchedule { get; private set; } //выбранная таблица
        public ExcelImportWindow()
        {
            
            InitializeComponent();
            LoadSchedules();
        }



        private Dictionary<string, ViewSchedule> Specifications = new Dictionary<string, ViewSchedule>();
        // Загрузка списка спецификаций из текущего документа Revit
        private void LoadSchedules()
        {
            try
            {
                
                Specifications.Clear();

                Document doc = RevitAPI.Document;
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                ICollection<ViewSchedule> schedules = collector
                    .OfClass(typeof(ViewSchedule))
                    .Cast<ViewSchedule>()
                    .Where(vs => vs.IsTemplate == false)
                    .OrderBy(vs => vs.Name)
                    .ToList();

                ScheduleComboBox.Items.Clear();
                foreach (var schedule in schedules)
                {
                    ScheduleComboBox.Items.Add(schedule.Name);
                    Specifications[schedule.Name] = schedule;
                }

                //затем добавляем пользовательские спецухм


                if (ScheduleComboBox.Items.Count > 0)
                {
                    ScheduleComboBox.IsEnabled = true;
                    ScheduleComboBox.SelectedIndex = 0;

                    ExportDataButton.IsEnabled = true;
                    ImportDataButton.IsEnabled = true;
                }
                else
                {
                    StatusTextBlock.Text = "В проекте нет спецификаций. Создайте спецификацию перед импортом.";
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Ошибка загрузки спецификаций: {ex.Message}";
            }
        }
        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Excel файлы|*.xlsx;*.xls|Все файлы|*.*",
                Title = "Выберите Excel файл"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                FilePathTextBox.Text = filePath;
                LoadSheets(filePath);
            }
        }

        private void LoadSheets(string filePath)
        {
            try
            {
                StatusTextBlock.Text = "Загрузка списка листов...";
                SheetComboBox.Items.Clear();
                SheetComboBox.IsEnabled = false;
                
                

                using (Workbook workbook = new Workbook())
                {
                    workbook.LoadFromFile(filePath);
                    var worksheets = workbook.Worksheets;

                    if (worksheets.Count == 0)
                    {
                        StatusTextBlock.Text = "В файле нет листов.";
                        return;
                    }

                    foreach (Worksheet sheet in worksheets)
                    {
                        SheetComboBox.Items.Add(sheet.Name);
                    }

                    if (SheetComboBox.Items.Count > 0)
                    {
                        
                        SheetComboBox.SelectedIndex = 0;
                        SheetComboBox.IsEnabled = true;
                       
                    }

                    StatusTextBlock.Text = $"Найдено листов: {worksheets.Count}. Нажмите «Импорт данных» для загрузки.";
                   
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Ошибка чтения файла: {ex.Message}";
              
            }
        }


       
       

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private ExcellLoadData excellLoadData = null;
        // Импорт данных из выбранного листа Excel в DataTable
        // Запись данных в выбранную спецификацию Revit
        private void ImportDataButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(FilePathTextBox.Text) || SheetComboBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите файл Excel и лист.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedFilePath = FilePathTextBox.Text;

            SelectedSheetName = SheetComboBox.SelectedItem.ToString();


            try
            {
                //загрузка данных
                excellLoadData = new ExcellLoadData(SelectedFilePath, SelectedSheetName);

            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Ошибка импорта данных: {ex.Message}";

            }

            if (excellLoadData == null || excellLoadData.ValuesCorrect.Count == 0)
            {
                MessageBox.Show("Нет импортированных данных. Сначала выполните «Импорт данных».", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (ScheduleComboBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите спецификацию Revit для записи.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Document doc = RevitAPI.Document;

            string scheduleName = ScheduleComboBox.SelectedItem.ToString();

            if (scheduleName!=null && Specifications.TryGetValue(scheduleName, out ViewSchedule schedule) && schedule != null && schedule.IsTemplate == false)
            {
            }
            else
            {
                MessageBox.Show("Выбранная спецификация не найдена в проекте.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

           
            //SelectedSchedule = targetSchedule;

            // Здесь ваша логика переноса данных из _excelData в спецификацию
            // Пример: создание элементов, заполнение параметров и т.д.
            // Так как это зависит от структуры спецификации, оставляем заглушку.
            try
            {
                // TODO: реализовать запись данных в спецификацию
                // Например, для каждой строки DataTable создавать элемент и заполнять параметры.
                var importClass = new ImportExcel();
                importClass.AddManualHeaderRow(schedule, excellLoadData);
               
                MessageBox.Show($"Данные спецификации успешно импортированы в :{scheduleName}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка записи данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        
        private void WriterSpecData(ViewSchedule schedule)
        {
            //мы добавляем к строкам строки

        }






        private void InoneButton_Click(object sender, RoutedEventArgs e) { }

        // ========== ЭКСПОРТ ДАННЫХ ИЗ SPECIFICATION REVIT В EXCEL ==========
        private void ExportDataButton_Click(object sender, RoutedEventArgs e)
        {
            if (ScheduleComboBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите спецификацию Revit для экспорта.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            

            Document doc = RevitAPI.Document;
            string scheduleName = ScheduleComboBox.SelectedItem.ToString();
            
            
            
            if (scheduleName != null && Specifications.TryGetValue(scheduleName, out ViewSchedule schedule) && schedule != null && schedule.IsTemplate == false)
            {
            }
            else
            {
                MessageBox.Show("Выбранная спецификация не найдена в проекте.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            

            // Диалог сохранения файла
            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "Excel файлы|*.xlsx",
                Title = "Сохранить экспорт спецификации как",
                FileName = $"{scheduleName}_export.xlsx"
            };
            if (saveDialog.ShowDialog() != true) return;

            string exportPath = saveDialog.FileName;

            try
            {
                StatusTextBlock.Text = "Экспорт данных из спецификации Revit...";
                ExportScheduleToExcel(schedule, exportPath);
                StatusTextBlock.Text = $"Экспорт завершён. Файл сохранён: {exportPath}";
                MessageBox.Show($"Данные спецификации успешно экспортированы в файл:\n{exportPath}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Ошибка экспорта: {ex.Message}";
                MessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ExportScheduleToExcel(ViewSchedule schedule, string filePath)
        {
            // Получаем данные спецификации
            TableData tableData = schedule.GetTableData();

            // Получаем секции: заголовки и тело
            TableSectionData headerSection = tableData.GetSectionData(SectionType.Header);
            TableSectionData bodySection = tableData.GetSectionData(SectionType.Body);

            //у заголовка ищем 
            int columnCountHeader = headerSection.NumberOfColumns;
            int rowCountHeader = headerSection.NumberOfRows;

            // Количество столбцов и строк
            int columnCount = bodySection.NumberOfColumns;
            int rowCount = bodySection.NumberOfRows;

            if (rowCount == 0)
                throw new Exception("Спецификация не содержит данных.");

            using (Workbook workbook = new Workbook())
            {
                workbook.Worksheets.Clear();
                Worksheet sheet = workbook.Worksheets.Add(schedule.Name);

                int row= 1;
                int relativeRow = 0;
                // Записываем заголовки столбцов (используем первую строку заголовка)
                for (relativeRow=0; relativeRow < rowCountHeader; relativeRow++)
                {
                    row = relativeRow + 1;
                    for (int col = 0; col < columnCountHeader; col++)
                    {

                        string headerText = GetCellText(schedule, SectionType.Header, relativeRow, col);
                        if (string.IsNullOrEmpty(headerText))
                        {
                            continue;
                        }

                        sheet.Range[row, col + 1].Text = headerText;
                    }
                }
                
                // Записываем данные строк
                for (relativeRow = 0; relativeRow < rowCount; relativeRow++)
                {
                    row++;
                    for (int col = 0; col < columnCount; col++)
                    {
                        string cellValue = GetCellText(schedule, SectionType.Body, relativeRow, col);
                        if (string.IsNullOrEmpty(cellValue))
                        {
                            continue;
                        }
                        sheet.Range[row, col + 1].Text = cellValue;
                    }
                }

                // Автоширина колонок
                sheet.AllocatedRange.AutoFitColumns();
                workbook.SaveToFile(filePath, ExcelVersion.Version2016);
            }
        }

        // Вспомогательный метод для получения текста ячейки
        private string GetCellText(ViewSchedule schedule, SectionType sectionType, int row, int column)
        {
            try
            {
                // Используем публичный метод GetCellText
                return schedule.GetCellText(sectionType, row, column) ?? "";
            }
            catch
            {
                return "";
            }
        }
        
       

    }


}
