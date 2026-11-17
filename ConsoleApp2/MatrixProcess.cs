using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

public sealed record MatrixTransfer(Matrix A, Matrix B, double TransferMilliseconds);

public static class MatrixProcess
{
    public static void Run(int n, int m, int k, int threads)
    {
        MatrixTransfer transfer = ReceiveMatrices(n, m, k);
        Console.WriteLine($"Получены матрицы {n}x{m} и {m}x{k}.");
        Console.WriteLine($"Время получения данных по каналу: {transfer.TransferMilliseconds:F2} мс");

        Stopwatch timer = Stopwatch.StartNew();
        Matrix result = MatrixMultiplier.MultiplyParallel(
            transfer.A, transfer.B, threads, true);
        timer.Stop();

        Console.WriteLine($"Время умножения: {timer.Elapsed.TotalMilliseconds:F2} мс");
        Console.WriteLine($"Контрольная сумма результата: {result.Checksum()}");
    }

    // Запуск отдельного процесса-генератора и чтение его stdout через pipe.
    public static MatrixTransfer ReceiveMatrices(int n, int m, int k)
    {
        if (n < 1 || m < 1 || k < 1)
            throw new ArgumentException("Размеры матриц должны быть положительными.");

        ProcessStartInfo info = CreateGeneratorStartInfo(n, m, k);
        using Process process = Process.Start(info)
            ?? throw new InvalidOperationException("Не удалось запустить процесс-генератор.");

        string? header = process.StandardOutput.ReadLine();
        if (header == null)
        {
            string errorText = process.StandardError.ReadToEnd();
            process.WaitForExit();
            throw new InvalidDataException("Генератор не передал заголовок. " + errorText);
        }

        string[] fields = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5)
            throw new InvalidDataException("Неверный формат заголовка IPC.");

        int rowsA = int.Parse(fields[0], CultureInfo.InvariantCulture);
        int columnsA = int.Parse(fields[1], CultureInfo.InvariantCulture);
        int columnsB = int.Parse(fields[2], CultureInfo.InvariantCulture);
        ulong expectedA = ulong.Parse(fields[3], CultureInfo.InvariantCulture);
        ulong expectedB = ulong.Parse(fields[4], CultureInfo.InvariantCulture);

        if (rowsA != n || columnsA != m || columnsB != k)
            throw new InvalidDataException("Размеры матриц в заголовке не совпадают с запросом.");

        // Заголовок уже принят. Замеряем получение полезной нагрузки (строки A и B),
        // не включая запуск процесса и генерацию, выполненную до отправки заголовка.
        Stopwatch transferTimer = Stopwatch.StartNew();
        process.StandardInput.WriteLine("GO");
        process.StandardInput.Flush();
        Matrix a = ReadMatrix(process.StandardOutput, rowsA, columnsA);
        Matrix b = ReadMatrix(process.StandardOutput, columnsA, columnsB);
        transferTimer.Stop();

        process.WaitForExit();
        string error = process.StandardError.ReadToEnd();
        if (process.ExitCode != 0)
            throw new InvalidOperationException("Процесс-генератор завершился с ошибкой: " + error);

        if (a.Fingerprint() != expectedA || b.Fingerprint() != expectedB)
            throw new InvalidDataException("Отпечатки принятых матриц не совпадают с отправленными.");

        return new MatrixTransfer(a, b, transferTimer.Elapsed.TotalMilliseconds);
    }

    private static ProcessStartInfo CreateGeneratorStartInfo(int n, int m, int k)
    {
        string executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("Не удалось определить путь к исполняемому файлу.");

        ProcessStartInfo info = new ProcessStartInfo
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        // При запуске через `dotnet имя.dll` ProcessPath может указывать на dotnet.exe.
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            string assemblyPath = Assembly.GetEntryAssembly()?.Location
                ?? throw new InvalidOperationException("Не удалось определить путь к DLL приложения.");
            info.FileName = executable;
            info.ArgumentList.Add(assemblyPath);
        }
        else
        {
            info.FileName = executable;
        }

        info.ArgumentList.Add("--generate");
        info.ArgumentList.Add(n.ToString(CultureInfo.InvariantCulture));
        info.ArgumentList.Add(m.ToString(CultureInfo.InvariantCulture));
        info.ArgumentList.Add(k.ToString(CultureInfo.InvariantCulture));
        return info;
    }

    private static Matrix ReadMatrix(StreamReader reader, int rows, int columns)
    {
        Matrix matrix = new Matrix(rows, columns);
        for (int i = 0; i < rows; i++)
        {
            string line = reader.ReadLine()
                ?? throw new EndOfStreamException("Передача матрицы оборвалась до получения всех строк.");
            string[] values = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (values.Length != columns)
                throw new InvalidDataException($"В строке {i} получено {values.Length} элементов вместо {columns}.");

            for (int j = 0; j < columns; j++)
                matrix[i, j] = long.Parse(values[j], CultureInfo.InvariantCulture);
        }
        return matrix;
    }

    // Точка входа дочернего процесса.
    public static void GenerateProcess(string[] args)
    {
        if (args.Length != 4)
            throw new ArgumentException("Ожидаются параметры --generate n m k.");

        int n = int.Parse(args[1], CultureInfo.InvariantCulture);
        int m = int.Parse(args[2], CultureInfo.InvariantCulture);
        int k = int.Parse(args[3], CultureInfo.InvariantCulture);
        Matrix a = Matrix.Generate(n, m);
        Matrix b = Matrix.Generate(m, k);

        Console.WriteLine($"{n} {m} {k} {a.Fingerprint().ToString(CultureInfo.InvariantCulture)} {b.Fingerprint().ToString(CultureInfo.InvariantCulture)}");
        Console.Out.Flush();
        if (Console.ReadLine() != "GO")
            throw new InvalidDataException("Родительский процесс не подтвердил готовность к приёму данных.");
        WriteMatrix(a);
        WriteMatrix(b);
    }

    private static void WriteMatrix(Matrix matrix)
    {
        StringBuilder line = new StringBuilder();
        for (int i = 0; i < matrix.Rows; i++)
        {
            line.Clear();
            for (int j = 0; j < matrix.Columns; j++)
            {
                if (j > 0) line.Append(' ');
                line.Append(matrix[i, j].ToString(CultureInfo.InvariantCulture));
            }
            Console.WriteLine(line.ToString());
        }
    }
}
