using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class Adm_GenError : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);

            // Включаем админ-режим на время теста — окно покажется.
            LookUsers.LookErrorsAdmin = true;

            try
            {
                // Меняем цифру на 1..5, чтобы выбрать нужный сценарий.
                int scenario = 1;
                GenerateScenario(scenario);
            }
            catch (Exception ex)
            {
                App_Apdater_1.LookUsers.LogError(ex);
                message = $"Adm_GenError: {ex.GetType().Name}: {ex.Message}";
                return Result.Failed;
            }

            return Result.Succeeded;
        }

        // Каждый сценарий — отдельный метод с [MethodImpl(NoInlining)],
        // чтобы JIT не «схлопнул» его со стеком вызывающего
        // и номер строки в логе показывал реальное место.

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void GenerateScenario(int n)
        {
            switch (n)
            {
                case 1: ThrowNullReference(); break;
                case 2: ThrowDivideByZero(); break;
                case 3: ThrowInLambda(); break;
                case 4: ThrowInLinqChain(); break;
                case 5: ThrowCustom(); break;
                default: ThrowNullReference(); break;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowNullReference()
        {
            object x = null;
            // Строка ниже — здесь будет NullReferenceException.
            // В логе ожидаем "ThrowNullReference at Adm_GenError.cs:line NN".
            Console.WriteLine(x.ToString());
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowDivideByZero()
        {
            int a = 10;
            int b = 0;
            int c = a / b;   // DivideByZeroException
            Console.WriteLine(c);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowInLambda()
        {
            var list = new List<int> { 1, 2, 0, 4 };

            // Лямбда — тот самый случай "<>c__DisplayClass...b__N"
            var result = list.Select(v => 100 / v).ToList();  // DivideByZeroException внутри
            Console.WriteLine(result.Count);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowInLinqChain()
        {
            // Типичный сценарий из вашего стека: LINQ + Where + ToList.
            var items = new[] { new WallStub("A"), null, new WallStub("B") };

            var ok = items
                .Where(x => x.Name.Length > 0)   // NullReferenceException в лямбде
                .ToList();

            Console.WriteLine(ok.Count);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowCustom()
        {
            throw new InvalidOperationException("Тестовая ошибка из Adm_GenError");
        }

        // Простейший заглушечный класс — чтобы не тянуть настоящий Wall
        private sealed class WallStub
        {
            public string Name { get; }
            public WallStub(string name) { Name = name; }
        }
    }
}