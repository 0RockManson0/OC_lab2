using System;
using System.Threading;

public static class MatrixMultiplier
{
    // Последовательное умножение. По умолчанию используется порядок i-k-j.
    public static Matrix MultiplySequential(Matrix a, Matrix b, bool useIkj = true)
    {
        CheckDimensions(a, b);
        Matrix result = new Matrix(a.Rows, b.Columns);

        for (int i = 0; i < a.Rows; i++)
        {
            if (useIkj)
            {
                for (int k = 0; k < a.Columns; k++)
                {
                    long value = a[i, k];
                    for (int j = 0; j < b.Columns; j++)
                        result[i, j] += value * b[k, j];
                }
            }
            else
            {
                for (int j = 0; j < b.Columns; j++)
                {
                    long sum = 0;
                    for (int k = 0; k < a.Columns; k++)
                        sum += a[i, k] * b[k, j];
                    result[i, j] = sum;
                }
            }
        }

        return result;
    }

    public static Matrix MultiplyParallel(
        Matrix a, Matrix b, int threadCount,
        bool showProgress = true, bool useIkj = true)
    {
        CheckDimensions(a, b);
        if (threadCount < 1)
            throw new ArgumentException("Количество потоков должно быть положительным.");

        Matrix result = new Matrix(a.Rows, b.Columns);
        Thread[] threads = new Thread[threadCount];
        int completedRows = 0;

        // Непрерывные блоки строк. Остаток распределяется по одному ряду
        // между первыми потоками. При T > n часть потоков получит 0 строк.
        int rowsPerThread = a.Rows / threadCount;
        int remainder = a.Rows % threadCount;

        for (int t = 0; t < threadCount; t++)
        {
            int start = t * rowsPerThread + Math.Min(t, remainder);
            int count = rowsPerThread + (t < remainder ? 1 : 0);
            int end = start + count;

            threads[t] = new Thread(() =>
            {
                for (int i = start; i < end; i++)
                {
                    if (useIkj)
                    {
                        for (int k = 0; k < a.Columns; k++)
                        {
                            long value = a[i, k];
                            for (int j = 0; j < b.Columns; j++)
                                result[i, j] += value * b[k, j];
                        }
                    }
                    else
                    {
                        for (int j = 0; j < b.Columns; j++)
                        {
                            long sum = 0;
                            for (int k = 0; k < a.Columns; k++)
                                sum += a[i, k] * b[k, j];
                            result[i, j] = sum;
                        }
                    }

                    Interlocked.Increment(ref completedRows);
                }
            });
            threads[t].Start();
        }

        if (showProgress)
        {
            int lastPercent = -1;
            while (Volatile.Read(ref completedRows) < a.Rows)
            {
                int percent = Volatile.Read(ref completedRows) * 100 / a.Rows;
                if (percent != lastPercent)
                {
                    Console.Write($"\rПрогресс: {percent}%   ");
                    lastPercent = percent;
                }
                Thread.Sleep(100);
            }
        }

        foreach (Thread thread in threads) thread.Join();
        if (showProgress) Console.WriteLine("\rПрогресс: 100%   ");
        return result;
    }

    private static void CheckDimensions(Matrix a, Matrix b)
    {
        if (a.Columns != b.Rows)
            throw new ArgumentException("Количество столбцов A должно совпадать с количеством строк B.");
    }
}
