using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public  class ImportExcel
    {
        public  void AddManualHeaderRow(ViewSchedule schedule, ExcellLoadData excellLoadData)
        {
            Document doc = RevitAPI.Document;

            if(excellLoadData.ValuesCorrect.Count == 0) {return; }

            int maxColumn = excellLoadData.ValuesCorrect.Select(x=>x.column).Max();
            

            using (Transaction tx = new Transaction(doc, "Add Header Row"))
            {
                tx.Start();

                TableData tableData = schedule.GetTableData();
                TableSectionData headerSection = tableData.GetSectionData(SectionType.Header);

                // Вставка строки после первой (tsd.FirstRowNumber обычно 0 или 1)
                int newRowIndex = headerSection.LastRowNumber + 1;
                int numCols = headerSection.NumberOfColumns;
                int firstCol = headerSection.FirstColumnNumber;
                int pastRow = -1;
                foreach (var data in excellLoadData.ValuesCorrect)
                {

                    if (data.row != pastRow)
                    {
                        headerSection.InsertRow(newRowIndex);
                        newRowIndex++;
                        pastRow = data.row;
                    }

                    // Заполнение ячеек (n столбцов, начиная с первого)
                    int colSet = firstCol + data.column;  // col от 1 -> индекс от firstCol
                    if (colSet< numCols)
                    {
                        headerSection.SetCellText(newRowIndex - 1, colSet, data.data);
                    }

                }

                schedule.Document.Regenerate();
                tx.Commit();
                
            }
        }


    }
}
