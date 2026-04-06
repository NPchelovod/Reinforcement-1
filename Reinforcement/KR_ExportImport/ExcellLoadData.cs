using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Spire.Xls;

namespace Reinforcement
{
    internal class ExcellLoadData
    {

        public string SelectedFilePath { get; private set; }
        public string SelectedSheetName { get; private set; }
        public ExcellLoadData(string selectedFilePath, string selectedSheetName)
        {
            SelectedFilePath = selectedFilePath;
            SelectedSheetName = selectedSheetName;
            LoadDataExcell();
        }



        private void LoadDataExcell()
        {
            //загружаем данные листа
            using (Workbook workbook = new Workbook())
            {
                workbook.LoadFromFile(SelectedFilePath);
                var worksheets = workbook.Worksheets;


                //ищем наш лист
                 List<Worksheet> AllSheets = workbook.Worksheets
                .Cast<Worksheet>().ToList();

                foreach (var sheet in AllSheets)
                {
                    if(sheet.Name == SelectedSheetName)
                    {
                        LoadDataSheet(sheet);
                        break;
                    }
                }

            }
        }

        public  int maxEmptyRow = 2;
        public int maxEmptyColumn = 3;

        private List<(int row, int column, string data)> Values = new List<(int row, int column, string data)>();
        //приведённые к row=1, col=1
        public List<(int row, int column, string data)> ValuesCorrect = new List<(int row, int column, string data)>();

        private void LoadDataSheet(Worksheet sheet)
        {
            Values.Clear();
            ValuesCorrect.Clear();
            //ищем данные теперь начинаем идти
            int Down_border = sheet.LastRow;
            int lastColumn = sheet.LastColumn;

            int emptyRow = 0;
            int emptyCol = 0;
            for (int row = 1; row <= Down_border; row++)
            {
                emptyCol = 0;
                bool existColData=false;
                for (int col = 1; col <= lastColumn; col++)
                {
                    CellRange cell = sheet.Range[row, col];
                    if (cell == null || string.IsNullOrEmpty(cell.Value.ToString()))
                    {
                        emptyCol++;
                        if(emptyCol> maxEmptyColumn && existColData)
                        {
                            break;
                        }
                        continue;
                    }
                    emptyCol = 0;
                    existColData = true;

                    var value_cell = cell.Value.ToString();
                    // записываем данные
                    Values.Add((row, col, value_cell));

                }

                //обрубка по строкам
                if (existColData)
                {
                    emptyRow++;
                    if(emptyRow> maxEmptyRow && Values.Count>0)
                    {
                        break;
                    }
                }
                else
                {
                    emptyRow = 0;
                }
            }

            //выполняем сортировку по строкам и по колоннам затем по возрастанию
            Values = Values.OrderBy(x=>x.row).ThenBy(x=>x.column).ToList();

            int minRow = Values.Select(x => x.row).Min()-1;
            int minCol = Values.Select(x=>x.column).Min()-1;
            foreach (var val in Values)
            {
                ValuesCorrect.Add((val.row - minRow, val.column - minCol, val.data));
            }


        }
    }
}
