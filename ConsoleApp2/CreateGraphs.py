import csv
from pathlib import Path

import matplotlib.pyplot as plt

BASE_DIR = Path(__file__).resolve().parent
RESULTS_DIR = BASE_DIR / "Results"
GRAPHS_DIR = RESULTS_DIR / "Graphs"


def read_csv(name):
    path = RESULTS_DIR / name
    if not path.exists():
        print(f"Нет файла {path.name}. Сначала запустите соответствующий бенчмарк.")
        return []
    with path.open("r", encoding="utf-8-sig", newline="") as file:
        return list(csv.DictReader(file, delimiter=";"))


def number(row, key):
    return float(row[key].replace(",", "."))


def machine_info():
    values = {"PhysicalCores": 0, "LogicalProcessors": 0}
    path = RESULTS_DIR / "machine_info.txt"
    if path.exists():
        for line in path.read_text(encoding="utf-8").splitlines():
            if "=" in line:
                key, value = line.split("=", 1)
                if key in values:
                    try:
                        values[key] = int(value)
                    except ValueError:
                        pass
    return values


def save(fig, name):
    fig.tight_layout()
    fig.savefig(GRAPHS_DIR / name, dpi=160)
    plt.close(fig)
    print(f"Создан график: Graphs/{name}")


def matrix_graphs():
    rows = read_csv("matrix_benchmark.csv")
    if not rows:
        return
    sizes = sorted({int(row["Size"]) for row in rows})

    fig, ax = plt.subplots()
    for size in sizes:
        part = sorted((r for r in rows if int(r["Size"]) == size),
                      key=lambda r: int(r["Threads"]))
        ax.plot([int(r["Threads"]) for r in part],
                [number(r, "TimeMs") for r in part], marker="o", label=f"{size} x {size}")
    ax.set_title("Время многопоточного умножения матриц")
    ax.set_xlabel("Количество потоков")
    ax.set_ylabel("Время, мс")
    ax.grid(True)
    ax.legend()
    save(fig, "matrix_time.png")

    info = machine_info()
    fig, ax = plt.subplots()
    max_threads = max(int(r["Threads"]) for r in rows)
    for size in sizes:
        part = sorted((r for r in rows if int(r["Size"]) == size),
                      key=lambda r: int(r["Threads"]))
        ax.plot([int(r["Threads"]) for r in part],
                [number(r, "Speedup") for r in part], marker="o", label=f"{size} x {size}")
    ax.plot([1, max_threads], [1, max_threads], linestyle="--", label="Идеальное ускорение S=T")
    if info["PhysicalCores"] > 0:
        ax.axvline(info["PhysicalCores"], linestyle=":", label=f"Физические ядра: {info['PhysicalCores']}")
    if info["LogicalProcessors"] > 0:
        ax.axvline(info["LogicalProcessors"], linestyle="-.", label=f"Логические процессоры: {info['LogicalProcessors']}")
    ax.set_title("Ускорение многопоточного умножения")
    ax.set_xlabel("Количество потоков")
    ax.set_ylabel("Ускорение S = t(1) / t(T)")
    ax.grid(True)
    ax.legend()
    save(fig, "matrix_speedup.png")


def loop_order_graph():
    rows = read_csv("loop_order_comparison.csv")
    if not rows:
        return
    labels = [r["Order"] for r in rows]
    values = [number(r, "TimeMs") for r in rows]
    fig, ax = plt.subplots()
    ax.bar(labels, values)
    ax.set_title("Сравнение порядка циклов на самой большой матрице")
    ax.set_xlabel("Порядок вложенных циклов")
    ax.set_ylabel("Время, мс")
    ax.grid(axis="y")
    save(fig, "loop_order.png")


def triangular_graphs():
    rows = read_csv("triangular_benchmark.csv")
    if not rows:
        return
    for size in sorted({int(r["Size"]) for r in rows}):
        fig, ax = plt.subplots()
        for method in ("Static", "Dynamic"):
            part = sorted((r for r in rows if int(r["Size"]) == size and r["Method"] == method),
                          key=lambda r: int(r["Threads"]))
            if part:
                ax.plot([int(r["Threads"]) for r in part],
                        [number(r, "TimeMs") for r in part], marker="o", label=method)
        ax.set_title(f"Нижнетреугольное умножение: {size} x {size}")
        ax.set_xlabel("Количество потоков")
        ax.set_ylabel("Время, мс")
        ax.grid(True)
        ax.legend()
        save(fig, f"triangular_time_{size}.png")


def ipc_graph():
    rows = read_csv("ipc_benchmark.csv")
    if not rows:
        return
    labels = [str(r["Size"]) for r in rows]
    transfer = [number(r, "TransferMs") for r in rows]
    multiply = [number(r, "MultiplyMs") for r in rows]
    positions = list(range(len(rows)))
    width = 0.36
    fig, ax = plt.subplots()
    ax.bar([p - width / 2 for p in positions], transfer, width, label="Получение данных по pipe")
    ax.bar([p + width / 2 for p in positions], multiply, width, label="Многопоточное умножение")
    ax.set_xticks(positions, labels)
    ax.set_title("Время передачи матриц и их умножения")
    ax.set_xlabel("Размер квадратных матриц")
    ax.set_ylabel("Время, мс")
    ax.grid(axis="y")
    ax.legend()
    save(fig, "ipc_transfer_vs_multiply.png")


def power_graphs():
    rows = read_csv("power_benchmark.csv")
    if not rows:
        return
    rows = sorted(rows, key=lambda r: int(r["Threads"]))
    threads = [int(r["Threads"]) for r in rows]
    size = rows[0]["Size"]
    exponent = rows[0]["Exponent"]

    fig, ax = plt.subplots()
    ax.plot(threads, [number(r, "TimeMs") / 1000 for r in rows], marker="o")
    ax.set_title(f"Время возведения матрицы {size} x {size} в степень {exponent}")
    ax.set_xlabel("Количество потоков")
    ax.set_ylabel("Время, с")
    ax.grid(True)
    save(fig, "power_time.png")

    fig, ax = plt.subplots()
    ax.plot(threads, [number(r, "Speedup") for r in rows], marker="o", label="Полученное ускорение")
    ax.plot([min(threads), max(threads)], [min(threads), max(threads)],
            linestyle="--", label="Идеальное ускорение S=T")
    ax.set_title(f"Ускорение возведения матрицы в степень {exponent}")
    ax.set_xlabel("Количество потоков")
    ax.set_ylabel("Ускорение S = t(1) / t(T)")
    ax.grid(True)
    ax.legend()
    save(fig, "power_speedup.png")


def main():
    GRAPHS_DIR.mkdir(parents=True, exist_ok=True)
    matrix_graphs()
    loop_order_graph()
    triangular_graphs()
    ipc_graph()
    power_graphs()
    print(f"\nГрафики находятся в папке: {GRAPHS_DIR}")


if __name__ == "__main__":
    main()
