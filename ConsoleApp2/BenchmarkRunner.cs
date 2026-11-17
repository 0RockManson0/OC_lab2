using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

public static class BenchmarkRunner
{
    private const int Measurements = 5;
    private static string ResultsDirectory => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Results"));

    public static void Run()
    {
        Console.WriteLine("\nБЕНЧМАРКИ ЛАБОРАТОРНОЙ РАБОТЫ");
        Console.WriteLine("1 — Обычное умножение (3 размера, порядок циклов)");
        Console.WriteLine("2 — Треугольные матрицы (static / dynamic)");
        Console.WriteLine("3 — Возведение в степень (автоподбор времени >= 10 секунд)");
        Console.WriteLine("4 — Передача по pipe и умножение (3 размера)");
        Console.Write("Выбор: ");

        switch (Console.ReadLine())
        {
            case "1": RunMatrixBenchmark(); break;
            case "2": RunTriangularBenchmark(); break;
            case "3": RunPowerBenchmark(); break;
            case "4": RunIpcBenchmark(); break;
            default: Console.WriteLine("Неизвестный режим."); break;
        }
    }

    private static int[] GetThreadCounts()
    {
        int logical = Math.Max(1, Environment.ProcessorCount);
        return Enumerable.Range(1, logical * 2)
            .Concat(new[] { logical * 4, logical * 8 })
            .Distinct()
            .ToArray();
    }

    private static int ReadPhysicalCoreCount()
    {
        int logical = Math.Max(1, Environment.ProcessorCount);
        Console.WriteLine($"Логических процессоров обнаружено программой: {logical}");
        Console.Write("Введите число физических ядер (Диспетчер задач → ЦП): ");
        string? value = Console.ReadLine();
        if (int.TryParse(value, out int physical) && physical > 0)
            return physical;
        Console.WriteLine("Число физических ядер не задано; в файле будет записан 0.");
        return 0;
    }

    private static void RunMatrixBenchmark()
    {
        int physicalCores = ReadPhysicalCoreCount();
        int logicalCores = Math.Max(1, Environment.ProcessorCount);
        SaveMachineInfo(physicalCores, logicalCores);
        int[] sizes = { 300, 600, 1800 };
        int[] threadCounts = GetThreadCounts();
        List<string> csv = new() { "Size;Threads;TimeMs;Speedup;Efficiency;Checksum" };
        List<string> loopCsv = new() { "Size;Order;TimeMs" };
        SaveCsv("matrix_benchmark.csv", csv);
        SaveCsv("loop_order_comparison.csv", loopCsv);

        foreach (int size in sizes)
        {
            Console.WriteLine($"\n=== Обычное умножение: {size} x {size} ===");
            Matrix a = Matrix.Generate(size, size, 101 + size);
            Matrix b = Matrix.Generate(size, size, 202 + size);
            Matrix expected = MatrixMultiplier.MultiplySequential(a, b);
            double baseline = 0;

            foreach (int threads in threadCounts)
            {
                Console.WriteLine($"Измерение T={threads} ...");
                double time = AverageTime(() =>
                    MatrixMultiplier.MultiplyParallel(a, b, threads, false));
                Matrix result = MatrixMultiplier.MultiplyParallel(a, b, threads, false);
                if (!expected.ContentEquals(result))
                    throw new InvalidOperationException($"Ошибка результата при T={threads}, размер {size}.");
                if (threads == 1) baseline = time;
                double speedup = baseline / time;
                double efficiency = speedup / threads;
                csv.Add(string.Join(";", size, threads, F(time), F(speedup),
                    F(efficiency), result.Checksum()));
                SaveCsv("matrix_benchmark.csv", csv);
                Console.WriteLine($"T={threads,-4} {time,10:F2} мс");
            }

            if (size == sizes[^1])
            {
                Console.WriteLine("Сравнение порядка циклов на самом большом размере...");
                double ikj = AverageTime(() => MatrixMultiplier.MultiplySequential(a, b, true));
                double ijk = AverageTime(() => MatrixMultiplier.MultiplySequential(a, b, false));
                loopCsv.Add($"{size};i-k-j;{F(ikj)}");
                loopCsv.Add($"{size};i-j-k;{F(ijk)}");
                Console.WriteLine($"i-k-j: {ikj:F2} мс; i-j-k: {ijk:F2} мс");
            }
        }

        SaveCsv("matrix_benchmark.csv", csv);
        SaveCsv("loop_order_comparison.csv", loopCsv);
    }

    private static void RunTriangularBenchmark()
    {
        int[] sizes = { 300, 600, 1000 };
        int[] threadCounts = GetThreadCounts();
        List<string> csv = new() { "Size;Method;Threads;TimeMs;SequentialMs;Speedup;Efficiency;Checksum" };
        SaveCsv("triangular_benchmark.csv", csv);

        foreach (int size in sizes)
        {
            Console.WriteLine($"\n=== Треугольная матрица: {size} x {size} ===");
            TriangularMatrix a = TriangularMatrix.Generate(size, 303 + size);
            Matrix b = Matrix.Generate(size, size, 404 + size);
            Matrix expected = TriangularMatrixMultiplier.MultiplySequential(a, b);
            double sequentialTime = AverageTime(() => TriangularMatrixMultiplier.MultiplySequential(a, b));

            foreach (int threads in threadCounts)
            {
                RunMethod("Static", () => TriangularMatrixMultiplier.MultiplyStatic(a, b, threads));
                RunMethod("Dynamic", () => TriangularMatrixMultiplier.MultiplyDynamic(a, b, threads));

                void RunMethod(string method, Func<Matrix> calculation)
                {
                    Console.WriteLine($"{method}, T={threads} ...");
                    double time = AverageTime(calculation);
                    Matrix result = calculation();
                    if (!expected.ContentEquals(result))
                        throw new InvalidOperationException($"Ошибка {method}, T={threads}, размер {size}.");
                    double speedup = sequentialTime / time;
                    double efficiency = speedup / threads;
                    csv.Add(string.Join(";", size, method, threads, F(time),
                        F(sequentialTime), F(speedup), F(efficiency), result.Checksum()));
                    SaveCsv("triangular_benchmark.csv", csv);
                }
            }
        }
        SaveCsv("triangular_benchmark.csv", csv);
    }

    private static void RunPowerBenchmark()
    {
        int exponent;
        while (true)
        {
            Console.Write("Введите степень p для экспериментальной серии (не меньше 2): ");
            if (int.TryParse(Console.ReadLine(), out exponent) && exponent >= 2) break;
            Console.WriteLine("Введите целое число p >= 2.");
        }
        Console.WriteLine("Подбираем размер, при котором последовательное A^p занимает около 10–20 секунд.");
        int size = CalibratePowerSize(exponent);
        Matrix source = Matrix.Generate(size, size, 505 + size);
        Console.WriteLine($"Для серии фиксируем n={size}, p={exponent}. Все варианты T используют одну и ту же матрицу.");

        double sequentialTime = AverageTime(() => MatrixPowerCalculator.PowerSequential(source, exponent));
        Console.WriteLine($"Последовательное время (усечённое среднее 3 из 5): {sequentialTime / 1000.0:F2} с");
        if (sequentialTime < 10000)
            Console.WriteLine("ВНИМАНИЕ: на этой машине среднее получилось меньше 10 секунд. Укажи это в анализе и при необходимости увеличь n.");

        List<string> csv = new() { "Size;Exponent;Threads;TimeMs;SequentialMs;Speedup;Efficiency;Checksum" };
        SaveCsv("power_benchmark.csv", csv);
        int[] threadCounts = GetThreadCounts();
        Matrix expected = MatrixPowerCalculator.PowerSequential(source, exponent);
        foreach (int threads in threadCounts)
        {
            Console.WriteLine($"Измерение степени: T={threads} ...");
            double time = AverageTime(() => MatrixPowerCalculator.Power(source, exponent, threads, false));
            Matrix result = MatrixPowerCalculator.Power(source, exponent, threads, false);
            if (!expected.ContentEquals(result))
                throw new InvalidOperationException($"Результат A^{exponent} не совпал при T={threads}.");
            double speedup = sequentialTime / time;
            double efficiency = speedup / threads;
            csv.Add(string.Join(";", size, exponent, threads, F(time),
                F(sequentialTime), F(speedup), F(efficiency), result.Checksum()));
            SaveCsv("power_benchmark.csv", csv);
            Console.WriteLine($"T={threads,-4} {time / 1000.0,9:F2} с");
        }
        SaveCsv("power_benchmark.csv", csv);
    }

    private static int CalibratePowerSize(int exponent)
    {
        int size = 300;
        for (int attempt = 1; attempt <= 6; attempt++)
        {
            Matrix source = Matrix.Generate(size, size, 700 + size);
            Stopwatch timer = Stopwatch.StartNew();
            MatrixPowerCalculator.PowerSequential(source, exponent);
            timer.Stop();
            double seconds = timer.Elapsed.TotalSeconds;
            Console.WriteLine($"Пробный размер {size}: {seconds:F2} с");

            if (seconds >= 10 && seconds <= 20) return size;
            double target = 12.0;
            double factor = Math.Pow(target / Math.Max(seconds, 0.05), 1.0 / 3.0);
            size = Math.Clamp((int)Math.Ceiling(size * factor * 1.03), 100, 1800);
            if (size == 1800 && seconds < 10) break;
        }
        return size;
    }

    private static void RunIpcBenchmark()
    {
        int threads = Math.Max(1, Environment.ProcessorCount);
        int[] sizes = { 200, 400, 600 };
        List<string> csv = new() { "Size;Threads;TransferMs;MultiplyMs;Checksum" };
        SaveCsv("ipc_benchmark.csv", csv);

        foreach (int size in sizes)
        {
            Console.WriteLine($"\n=== IPC pipe: матрицы {size} x {size}, T={threads} ===");
            List<double> transferTimes = new();
            List<double> multiplyTimes = new();
            long checksum = 0;

            // 5 отдельных процессов и 5 измерений для каждого размера.
            for (int sample = 1; sample <= Measurements; sample++)
            {
                MatrixTransfer received = MatrixProcess.ReceiveMatrices(size, size, size);
                Stopwatch timer = Stopwatch.StartNew();
                Matrix result = MatrixMultiplier.MultiplyParallel(
                    received.A, received.B, threads, false);
                timer.Stop();
                transferTimes.Add(received.TransferMilliseconds);
                multiplyTimes.Add(timer.Elapsed.TotalMilliseconds);
                checksum = result.Checksum();
                Console.WriteLine($"Замер {sample}/{Measurements}: передача {received.TransferMilliseconds:F2} мс; умножение {timer.Elapsed.TotalMilliseconds:F2} мс");
            }

            csv.Add(string.Join(";", size, threads, F(TrimmedAverage(transferTimes)),
                F(TrimmedAverage(multiplyTimes)), checksum));
            SaveCsv("ipc_benchmark.csv", csv);
        }
        SaveCsv("ipc_benchmark.csv", csv);
    }

    private static double AverageTime(Func<Matrix> action)
    {
        action(); // Прогрев JIT, не входит в измерения.
        List<double> times = new();
        for (int i = 0; i < Measurements; i++)
        {
            Stopwatch timer = Stopwatch.StartNew();
            action();
            timer.Stop();
            times.Add(timer.Elapsed.TotalMilliseconds);
        }
        return TrimmedAverage(times);
    }

    private static double TrimmedAverage(List<double> values)
    {
        values.Sort();
        return values.Skip(1).Take(values.Count - 2).Average();
    }

    private static string F(double value) => value.ToString("F3", CultureInfo.InvariantCulture);

    private static void SaveMachineInfo(int physical, int logical)
    {
        Directory.CreateDirectory(ResultsDirectory);
        File.WriteAllText(Path.Combine(ResultsDirectory, "machine_info.txt"),
            $"PhysicalCores={physical}{Environment.NewLine}LogicalProcessors={logical}{Environment.NewLine}");
    }

    private static void SaveCsv(string fileName, List<string> lines)
    {
        Directory.CreateDirectory(ResultsDirectory);
        string path = Path.Combine(ResultsDirectory, fileName);
        File.WriteAllLines(path, lines);
        Console.WriteLine($"\nСохранено: {Path.GetFullPath(path)}");
    }
}
