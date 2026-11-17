using System;
using System.Diagnostics;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--generate")
        {
            try
            {
                MatrixProcess.GenerateProcess(args);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception.Message);
                Environment.ExitCode = 1;
            }
            return;
        }

        while (true)
        {
            Console.WriteLine("\n========================================");
            Console.WriteLine("       ЛАБОРАТОРНАЯ РАБОТА №2");
            Console.WriteLine("========================================");
            Console.WriteLine("1 — Обычное многопоточное умножение");
            Console.WriteLine("2 — Эксперименты и графики (бенчмарки)");
            Console.WriteLine("3 — Нижнетреугольная матрица (static / dynamic)");
            Console.WriteLine("4 — Генерация и передача матриц через pipe");
            Console.WriteLine("5 — Возведение матрицы в степень с барьером");
            Console.WriteLine("6 — Демонстрация ошибки без барьера");
            Console.WriteLine("0 — Выход");
            Console.Write("\nВыбор: ");

            string? choice = Console.ReadLine();
            try
            {
                switch (choice)
                {
                    case "1": RunMultiplication(); break;
                    case "2": BenchmarkRunner.Run(); break;
                    case "3": RunTriangular(); break;
                    case "4": RunProcess(); break;
                    case "5": RunPower(); break;
                    case "6": RunWithoutBarrierDemo(); break;
                    case "0": return;
                    default: Console.WriteLine("Неизвестный пункт меню."); break;
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("Ошибка: " + exception.Message);
            }

            Console.WriteLine("\nНажмите Enter для возврата в меню.");
            Console.ReadLine();
        }
    }

    private static void RunMultiplication()
    {
        int n = ReadNumber("Количество строк A (n): ");
        int m = ReadNumber("Количество столбцов A и строк B (m): ");
        int k = ReadNumber("Количество столбцов B (k): ");
        int threads = ReadNumber("Количество потоков T: ");

        Matrix a = Matrix.Generate(n, m);
        Matrix b = Matrix.Generate(m, k);
        Console.WriteLine("\nВыполняется умножение...");
        Stopwatch timer = Stopwatch.StartNew();
        Matrix result = MatrixMultiplier.MultiplyParallel(a, b, threads, true);
        timer.Stop();

        Console.WriteLine($"Время умножения: {timer.Elapsed.TotalMilliseconds:F2} мс");
        Console.WriteLine($"Контрольная сумма: {result.Checksum()}");
        if (n <= 10 && k <= 10)
        {
            Console.WriteLine("Матрица результата C:");
            result.Print();
        }
    }

    private static void RunTriangular()
    {
        int size = ReadNumber("Размер нижнетреугольной матрицы A (n): ");
        int columns = ReadNumber("Количество столбцов B (k): ");
        int threads = ReadNumber("Количество потоков T: ");
        Console.WriteLine("1 — Статическое распределение; 2 — Динамическое распределение");
        Console.Write("Способ: ");
        string? method = Console.ReadLine();

        TriangularMatrix a = TriangularMatrix.Generate(size);
        Matrix b = Matrix.Generate(size, columns);
        Stopwatch timer = Stopwatch.StartNew();
        Matrix result = method == "1"
            ? TriangularMatrixMultiplier.MultiplyStatic(a, b, threads)
            : TriangularMatrixMultiplier.MultiplyDynamic(a, b, threads);
        timer.Stop();

        Console.WriteLine($"Время: {timer.Elapsed.TotalMilliseconds:F2} мс");
        Console.WriteLine($"Контрольная сумма: {result.Checksum()}");
    }

    private static void RunProcess()
    {
        int n = ReadNumber("Количество строк A: ");
        int m = ReadNumber("Количество столбцов A и строк B: ");
        int k = ReadNumber("Количество столбцов B: ");
        int threads = ReadNumber("Количество потоков: ");
        MatrixProcess.Run(n, m, k, threads);
    }

    private static void RunPower()
    {
        int size = ReadNumber("Размер квадратной матрицы n: ");
        int exponent = ReadNumber("Степень p: ");
        int threads = ReadNumber("Количество потоков T: ");
        Matrix source = Matrix.Generate(size, size);

        Stopwatch timer = Stopwatch.StartNew();
        Matrix result = MatrixPowerCalculator.Power(source, exponent, threads, true);
        timer.Stop();

        Console.WriteLine($"Время возведения в степень: {timer.Elapsed.TotalMilliseconds:F2} мс");
        Console.WriteLine($"Контрольная сумма A^{exponent}: {result.Checksum()}");
        Console.WriteLine($"Вычисления выполнялись по модулю {MatrixPowerCalculator.Mod}.");
    }

    private static void RunWithoutBarrierDemo()
    {
        Console.WriteLine("Демонстрация запуска следующего шага до завершения предыдущего.");
        Matrix source = Matrix.Generate(8, 8, 42);
        Matrix correct = MatrixPowerCalculator.PowerSequential(source, 3);
        Matrix withoutBarrier = MatrixPowerCalculator.PowerWithoutBarrierDemo(source, 3, 2);

        Console.WriteLine($"Правильная контрольная сумма: {correct.Checksum()}");
        Console.WriteLine($"Без барьера:                 {withoutBarrier.Checksum()}");
        Console.WriteLine(correct.ContentEquals(withoutBarrier)
            ? "В этот раз суммы совпали, но порядок выполнения не гарантирован. Повторите запуск."
            : "Результаты различаются: второй поток ещё не закончил предыдущий шаг, когда первый начал следующий.");
    }

    private static int ReadNumber(string message)
    {
        while (true)
        {
            Console.Write(message);
            if (int.TryParse(Console.ReadLine(), out int value) && value > 0)
                return value;
            Console.WriteLine("Введите положительное целое число.");
        }
    }
}
