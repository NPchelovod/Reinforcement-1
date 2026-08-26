using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using View = Autodesk.Revit.DB.View;
namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class FopAdd : IExternalCommand
    {
        
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);

            RegParameter(commandData, AutoFillNoteUpdater.NameAvtor, BuiltInParameterGroup.PG_IDENTITY_DATA);
            RegParameter(commandData, AutoFillNoteUpdater.NameSoAvtor, BuiltInParameterGroup.PG_IDENTITY_DATA);
            return Result.Succeeded;
        }
        public static Definition GetDifinition(ExternalCommandData commandData, string name)
        {
            DefinitionFile sharedParamsFile = commandData.Application.Application.OpenSharedParameterFile();
            if (sharedParamsFile == null)
            {
                TaskDialog.Show("Ошибка", "Файл общих параметров не найден");
                return null;
            }
            foreach (DefinitionGroup group in sharedParamsFile.Groups)
            {
                foreach (Definition definition in group.Definitions)
                {
                    if (definition.Name == name)
                    {
                        return definition;
                    }
                }
            }
            return null;
        }

        public static bool RegParameter(ExternalCommandData commandData, string name, BuiltInParameterGroup builtInParameterGroup, bool allCat = true)
        {
            Definition definition = GetDifinition(commandData, name);
            if(definition == null) { return  false; }
            Document doc = RevitAPI.Document;

            // Проверяем, есть ли уже привязка для этого параметра
            // Способ 1: через итерацию BindingMap
            DefinitionBindingMapIterator it = doc.ParameterBindings.ForwardIterator();
            while (it.MoveNext())
            {
                if (it.Key.Name == name)
                {
                    // Параметр уже привязан – выходим
                    TaskDialog.Show("Информация", $"Параметр '{name}' уже привязан в проекте.");
                    return true; // или false, в зависимости от логики
                }
            }
            //CategorySet categories = commandData.Application.Application.Create.NewCategorySet();
            //foreach (var category in RegisterAutoFillUpdater.BuiltInCategorys)
            //{
            //    categories.Insert(doc.Settings.Categories.get_Item(category));
            //}



            CategorySet categories2 = GetCategories(commandData, doc, allCat);
            using (Transaction t = new Transaction(doc, "Привязка параметра"))
            {
                t.Start();

                
                //foreach (var category in RegisterAutoFillUpdater.BuiltInCategorys)
                //{
                //    categories.Insert(doc.Settings.Categories.get_Item(category));
                //}

                // Создаём привязку (InstanceBinding или TypeBinding)
                InstanceBinding binding = commandData.Application.Application.Create.NewInstanceBinding(categories2);
              
                // Вставляем определение в BindingMap проекта
                doc.ParameterBindings.Insert(definition, binding, builtInParameterGroup);

                t.Commit();
            }
            return true;

        }

        public static CategorySet GetCategories(ExternalCommandData commandData, Document doc, bool allCat=true)
        {
            CategorySet categories = commandData.Application.Application.Create.NewCategorySet();

            if (allCat)
            {
                Categories allCategories = doc.Settings.Categories;
                foreach (Category cat in allCategories)
                {
                    // Пропускаем категории, которые не могут иметь общие параметры
                    if (cat.AllowsBoundParameters)
                        categories.Insert(cat);
                }
            }
            else
            {
                foreach (var category in RegisterAutoFillUpdater.BuiltInCategorys)
                {
                    categories.Insert(doc.Settings.Categories.get_Item(category));
                }
            }
            return categories;
        }
    }
}
