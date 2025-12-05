using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{



    public class HelperSeachAllElements
    {

        //помогает найти элементы семейства например все сваи


        //здесь скрыты полные имена поисковые если мы раньше не нашли
        private static Dictionary<HashSet<string>, List<string>> newNames = new Dictionary<HashSet<string>, List<string>>();

        public static HashSet<Element> SeachAllElements(HashSet<string> names, ExternalCommandData commandData, bool activView=false)
        {
            //if activView - ищет все сваи на активном виде

            if (!newNames.TryGetValue(names, out var searchNames))
            {
                searchNames = names.ToList();
                newNames[names] = searchNames;
            }
            

            RevitAPI.Initialize(commandData);
            Document doc = RevitAPI.Document;
            UIDocument uiDoc = RevitAPI.UiDocument;
            FilteredElementCollector collection = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol));
            HashSet<Element> resultElements = new HashSet<Element>();
            int maxAttempts = 5;
            int attempt = 0;

            while (attempt < maxAttempts)
            {
                attempt++;

                // Ищем элементы
                resultElements = FindElementsByName(searchNames, doc, uiDoc, activView);

                if (resultElements.Count > 0)
                {
                    TaskDialog.Show("Найдено",
                        $"Найдено {resultElements.Count} элементов по именам: {string.Join(", ", searchNames)}");
                    break;
                }

                // Если не нашли - предлагаем варианты
                var dialogResult = ShowNotFoundDialog(searchNames, attempt);

                if (dialogResult.action == NotFoundAction.Cancel)
                {
                    break;
                }
                else if (dialogResult.action == NotFoundAction.InputText)
                {
                    // Ввод текста
                    string newName = ShowInputDialog("Введите имя семейства:");
                    if (!string.IsNullOrEmpty(newName))
                    {
                        searchNames.Add(newName);
                        newNames[names] = searchNames;
                        TaskDialog.Show("Добавлено",
                            $"Добавлено имя: {newName}\nВсего ищется: {string.Join(", ", searchNames)}");
                    }
                }
                else if (dialogResult.action == NotFoundAction.SelectElement)
                {
                    // Выбор элемента мышкой
                    try
                    {
                        var selectedFamilyName = SelectElementByMouse(uiDoc);
                        if (!string.IsNullOrEmpty(selectedFamilyName))
                        {
                            searchNames.Add(selectedFamilyName);
                            newNames[names] = searchNames;
                            TaskDialog.Show("Добавлено",
                                $"Добавлено имя из выбранного элемента: {selectedFamilyName}\n" +
                                $"Всего ищется: {string.Join(", ", searchNames)}");
                        }
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        TaskDialog.Show("Отменено", "Выбор элемента отменен пользователем");
                    }
                }
            }

            return resultElements;
        }

        private static HashSet<Element> FindElementsByName(List<string> searchNames, Document doc,
            UIDocument uiDoc, bool activeView)
        {
            FilteredElementCollector collector;

            if (activeView)
            {
                collector = new FilteredElementCollector(doc, uiDoc.ActiveView.Id);
            }
            else
            {
                collector = new FilteredElementCollector(doc);
            }

            return collector
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(instance =>
                    instance.Symbol != null &&
                    searchNames.Any(name =>
                        instance.Symbol.FamilyName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        instance.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        instance.Symbol.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0))
                .Cast<Element>()
                .ToHashSet();
        }

        private static (NotFoundAction action, string additionalInfo) ShowNotFoundDialog(List<string> searchNames, int attempt)
        {
            var dialog = new TaskDialog("Элементы не найдены");
            dialog.MainInstruction = $"Попытка {attempt}: Элементы по заданным именам не найдены.";
            dialog.MainContent = $"Искали: {string.Join(", ", searchNames)}";

            // Добавляем кнопки
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1,
                "📝 Ввести имя семейства текстом");
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2,
                "🖱️ Выбрать элемент мышкой");
            dialog.CommonButtons = TaskDialogCommonButtons.Cancel;

            var result = dialog.Show();

            if (result == TaskDialogResult.CommandLink1)
            {
                return (NotFoundAction.InputText, null);
            }
            else if (result == TaskDialogResult.CommandLink2)
            {
                return (NotFoundAction.SelectElement, null);
            }

            return (NotFoundAction.Cancel, null);
        }

        private static string SelectElementByMouse(UIDocument uiDoc)
        {
            // Создаем диалог для инструкции
            var instructionDialog = new TaskDialog("Выбор элемента");
            instructionDialog.MainInstruction = "Выберите элемент семейства на экране";
            instructionDialog.MainContent = "Нажмите OK, затем кликните на любой элемент семейства";
            instructionDialog.CommonButtons = TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel;

            if (instructionDialog.Show() != TaskDialogResult.Ok)
            {
                return null;
            }

            try
            {
                // Позволяем пользователю выбрать элемент
                var reference = uiDoc.Selection.PickObject(
                    Autodesk.Revit.UI.Selection.ObjectType.Element,
                    "Выберите элемент семейства");

                var element = uiDoc.Document.GetElement(reference);

                if (element is FamilyInstance familyInstance && familyInstance.Symbol != null)
                {
                    // Возвращаем разные варианты имен
                    var familyName = familyInstance.Symbol.FamilyName;
                    var symbolName = familyInstance.Name;
                    var typeName = familyInstance.Symbol.Name;

                    // Показываем диалог выбора имени
                    var nameSelectDialog = new TaskDialog("Выберите имя для поиска");
                    nameSelectDialog.MainInstruction = "Какое имя использовать для поиска?";

                    nameSelectDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1,
                        $"Имя семейства: {familyName}");
                    nameSelectDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2,
                        $"Имя типа: {typeName}");
                    nameSelectDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink3,
                        $"Имя экземпляра: {symbolName}");
                    nameSelectDialog.CommonButtons = TaskDialogCommonButtons.Cancel;

                    var nameResult = nameSelectDialog.Show();

                    if (nameResult == TaskDialogResult.CommandLink1)
                        return familyName;
                    else if (nameResult == TaskDialogResult.CommandLink2)
                        return typeName;
                    else if (nameResult == TaskDialogResult.CommandLink3)
                        return symbolName;
                }
                else
                {
                    TaskDialog.Show("Ошибка", "Выбранный элемент не является экземпляром семейства");
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                throw; // Пробрасываем дальше для обработки
            }

            return null;
        }

        private static string ShowInputDialog(string prompt)
        {
            // Используем Windows Forms для простого ввода
            System.Windows.Forms.Form promptForm = new System.Windows.Forms.Form()
            {
                Width = 400,
                Height = 200,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "Ввод имени",
                StartPosition = FormStartPosition.CenterScreen
            };

            System.Windows.Forms.Label textLabel = new System.Windows.Forms.Label()
            {
                Left = 20,
                Top = 20,
                Width = 360,
                Text = prompt,
                Font = new System.Drawing.Font("Arial", 10)
            };

            System.Windows.Forms.TextBox textBox = new System.Windows.Forms.TextBox()
            {
                Left = 20,
                Top = 50,
                Width = 360,
                Font = new System.Drawing.Font("Arial", 10)
            };

            System.Windows.Forms.Button confirmation = new System.Windows.Forms.Button()
            {
                Text = "OK",
                Left = 150,
                Width = 100,
                Top = 100,
                DialogResult = DialogResult.OK
            };

            confirmation.Click += (sender, e) => { promptForm.Close(); };

            promptForm.Controls.Add(textBox);
            promptForm.Controls.Add(confirmation);
            promptForm.Controls.Add(textLabel);

            promptForm.AcceptButton = confirmation;

            if (promptForm.ShowDialog() == DialogResult.OK)
            {
                return textBox.Text.Trim();
            }

            return null;
        }

        // Альтернативная версия с WPF (если предпочитаете)
        private static string ShowInputDialogWpf(string prompt)
        {
            var dialog = new System.Windows.Window()
            {
                Title = "Ввод имени",
                Width = 300,
                Height = 150,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen
            };

            var stackPanel = new System.Windows.Controls.StackPanel();

            var textBlock = new System.Windows.Controls.TextBlock
            {
                Text = prompt,
                Margin = new System.Windows.Thickness(10),
                TextWrapping = System.Windows.TextWrapping.Wrap
            };

            var textBox = new System.Windows.Controls.TextBox
            {
                Margin = new System.Windows.Thickness(10),
                Height = 25
            };

            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };

            var okButton = new System.Windows.Controls.Button
            {
                Content = "OK",
                Width = 80,
                Margin = new System.Windows.Thickness(5)
            };

            var cancelButton = new System.Windows.Controls.Button
            {
                Content = "Cancel",
                Width = 80,
                Margin = new System.Windows.Thickness(5)
            };

            string result = null;

            okButton.Click += (s, e) =>
            {
                result = textBox.Text;
                dialog.DialogResult = true;
            };

            cancelButton.Click += (s, e) =>
            {
                dialog.DialogResult = false;
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            stackPanel.Children.Add(textBlock);
            stackPanel.Children.Add(textBox);
            stackPanel.Children.Add(buttonPanel);

            dialog.Content = stackPanel;

            if (dialog.ShowDialog() == true)
            {
                return result;
            }

            return null;
        }
    }

    internal enum NotFoundAction
    {
        Cancel,
        InputText,
        SelectElement
    }
}

    


