using System;
using System.Threading;

public static class MatrixPowerCalculator
{
    public const long Mod = 1_000_000_007;

    public static Matrix Power(Matrix source, int exponent, int threadCount, bool showProgress = true)
    {
        Check(source, exponent, threadCount);
        int n = source.Rows;
        Matrix current = Normalize(source);
        if (exponent == 1)
        {
            if (showProgress) Console.WriteLine("Прогресс: 100% (степень 1 — умножений нет)");
            return current;
        }

        Matrix next = new Matrix(n, n);
        int completedRows = 0;
        int totalRows = checked((exponent - 1) * n);

        // Барьер выполняет обмен буферов только после того, как все потоки
        // закончили текущую степень. Потоки создаются один раз на всю работу.
        using Barrier barrier = new Barrier(threadCount, _ =>
        {
            Matrix temp = current;
            current = next;
            next = temp;
        });

        Thread[] workers = new Thread[threadCount];
        int rowsPerThread = n / threadCount;
        int remainder = n % threadCount;

        for (int t = 0; t < threadCount; t++)
        {
            int start = t * rowsPerThread + Math.Min(t, remainder);
            int count = rowsPerThread + (t < remainder ? 1 : 0);
            int end = start + count;

            workers[t] = new Thread(() =>
            {
                for (int step = 1; step < exponent; step++)
                {
                    // Эти ссылки одинаковы у всех участников на старте фазы.
                    Matrix input = current;
                    Matrix output = next;

                    for (int i = start; i < end; i++)
                    {
                        for (int j = 0; j < n; j++)
                        {
                            long sum = 0;
                            for (int k = 0; k < n; k++)
                            {
                                // Каждый член приводится по модулю сразу,
                                // поэтому сумма не переполняет Int64.
                                sum = (sum + (input[i, k] * input[k, j]) % Mod) % Mod;
                            }
                            output[i, j] = sum;
                        }
                        Interlocked.Increment(ref completedRows);
                    }

                    barrier.SignalAndWait();
                }
            });
            workers[t].Start();
        }

        if (showProgress)
        {
            int lastPercent = -1;
            while (Volatile.Read(ref completedRows) < totalRows)
            {
                int percent = Volatile.Read(ref completedRows) * 100 / totalRows;
                if (percent != lastPercent)
                {
                    Console.Write($"\rПрогресс: {percent}%   ");
                    lastPercent = percent;
                }
                Thread.Sleep(100);
            }
        }

        foreach (Thread worker in workers) worker.Join();
        if (showProgress) Console.WriteLine("\rПрогресс: 100%   ");
        return current;
    }

    // Последовательная версия использует те же два буфера и ту же формулу.
    public static Matrix PowerSequential(Matrix source, int exponent)
    {
        if (source.Rows != source.Columns)
            throw new ArgumentException("Матрица должна быть квадратной.");
        if (exponent < 1)
            throw new ArgumentException("Степень должна быть не меньше 1.");

        int n = source.Rows;
        Matrix current = Normalize(source);
        Matrix next = new Matrix(n, n);

        for (int step = 1; step < exponent; step++)
        {
            MultiplyRows(current, next, 0, n);
            Matrix temp = current;
            current = next;
            next = temp;
        }
        return current;
    }

    // Демонстрация ошибки при отсутствии барьера. Первый поток намеренно
    // опережает второй: он начинает следующий шаг, пока чужие строки
    // предыдущего произведения ещё не вычислены.
    public static Matrix PowerWithoutBarrierDemo(Matrix source, int exponent, int threadCount)
    {
        Check(source, exponent, threadCount);
        if (exponent < 2 || threadCount < 2)
            throw new ArgumentException("Для демонстрации нужны степень >= 2 и минимум 2 потока.");

        int n = source.Rows;
        Matrix first = Normalize(source);
        Matrix second = new Matrix(n, n);
        Matrix? result = null;
        Thread[] workers = new Thread[threadCount];
        int rowsPerThread = n / threadCount;
        int remainder = n % threadCount;

        for (int t = 0; t < threadCount; t++)
        {
            int workerId = t;
            int start = t * rowsPerThread + Math.Min(t, remainder);
            int count = rowsPerThread + (t < remainder ? 1 : 0);
            int end = start + count;

            workers[t] = new Thread(() =>
            {
                Matrix localCurrent = first;
                Matrix localNext = second;

                for (int step = 1; step < exponent; step++)
                {
                    if (workerId > 0 && step == 1)
                        Thread.Sleep(200); // Создаём воспроизводимое опережение.

                    MultiplyRows(localCurrent, localNext, start, end);
                    Matrix temp = localCurrent;
                    localCurrent = localNext;
                    localNext = temp;
                }

                if (workerId == 0) result = localCurrent;
            });
            workers[t].Start();
        }

        foreach (Thread worker in workers) worker.Join();
        return result ?? throw new InvalidOperationException("Не получен результат демонстрации.");
    }

    private static void MultiplyRows(Matrix input, Matrix output, int start, int end)
    {
        int n = input.Rows;
        for (int i = start; i < end; i++)
        for (int j = 0; j < n; j++)
        {
            long sum = 0;
            for (int k = 0; k < n; k++)
                sum = (sum + (input[i, k] * input[k, j]) % Mod) % Mod;
            output[i, j] = sum;
        }
    }

    private static Matrix Normalize(Matrix source)
    {
        Matrix result = new Matrix(source.Rows, source.Columns);
        for (int i = 0; i < source.Rows; i++)
        for (int j = 0; j < source.Columns; j++)
            result[i, j] = ((source[i, j] % Mod) + Mod) % Mod;
        return result;
    }

    private static void Check(Matrix source, int exponent, int threadCount)
    {
        if (source.Rows != source.Columns)
            throw new ArgumentException("Матрица должна быть квадратной.");
        if (exponent < 1)
            throw new ArgumentException("Степень должна быть не меньше 1.");
        if (threadCount < 1)
            throw new ArgumentException("Количество потоков должно быть положительным.");
    }
}
