using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VendingMachineApp
{
    public static class Denominations
    {
        public static readonly int[] All = new int[]
        {
            500000, 200000, 100000, 50000, 20000, 10000, 5000, 1000, 500, 200, 100
        };
    }

    public class Product
    {
        public string Code { get; }
        public string Name { get; set; }
        public int PriceKops { get; set; }
        public int Quantity { get; set; }
         public Product(string code, string name, int priceRubles, int quantity)
        {
            Code = code;
            Name = name;
            PriceKops = priceRubles * 100;
            Quantity = quantity;
        }
    }

    public class Transaction
    {
        public Dictionary<int, int> Inserted { get; } = new Dictionary<int, int>();
        public void Insert(int denomKops)
        {
            if (!Inserted.ContainsKey(denomKops)) Inserted[denomKops] = 0;
            Inserted[denomKops]++;
        }

        public int TotalKops()
        {
            return Inserted.Sum(kv => kv.Key * kv.Value);
        }
        public void Clear() => Inserted.Clear();
    }

    public class VendingMachine
    {
        public Dictionary<string, Product> Products { get; } = new Dictionary<string, Product>();
        public Dictionary<int, int> CashInventory { get; } = new Dictionary<int, int>();
        public int CollectedKops { get; private set; } = 0;

        public Transaction CurrentTransaction { get; } = new Transaction();

        public VendingMachine()
        {
            foreach (var d in Denominations.All)
                CashInventory[d] = 0;
        }

        public void AddOrUpdateProduct(Product p)
        {
            if (Products.ContainsKey(p.Code))
            {
                Products[p.Code].Quantity += p.Quantity;
                Products[p.Code].Name = p.Name;
                Products[p.Code].PriceKops = p.PriceKops;
            }
            else Products[p.Code] = p;
        }

        public void AddCash(int denomKops, int count)
        {
            if (!CashInventory.ContainsKey(denomKops)) CashInventory[denomKops] = 0;
            CashInventory[denomKops] += count;
        }

        public Dictionary<int, int> MakeChange(int changeKops, Dictionary<int, int> availableForChange)
        {
            if (changeKops == 0) return new Dictionary<int, int>();
            var result = new Dictionary<int, int>();
            int remaining = changeKops;
            foreach (var denom in Denominations.All)
            {
                if (denom > remaining) continue;
                int availCount = availableForChange.ContainsKey(denom) ? availableForChange[denom] : 0;
                if (availCount <= 0) continue;
                int need = remaining / denom;
                int take = Math.Min(need, availCount);
                if (take > 0)
                {
                    result[denom] = take;
                    remaining -= denom * take;
                }
                if (remaining == 0) break;
            }
            if (remaining != 0) return null;
            return result;
        }

        public (bool, string, Dictionary<int, int>) TryPurchase(string productCode)
        {
            if (!Products.ContainsKey(productCode)) return (false, "Товар с таким кодом не найден.", null);
            var product = Products[productCode];
            if (product.Quantity <= 0) return (false, "Товар закончился.", null);
            int inserted = CurrentTransaction.TotalKops();
            if (inserted < product.PriceKops) return (false, $"Недостаточно средств. Нужно {Fmt(product.PriceKops)}.", null);
            int change = inserted - product.PriceKops;

            var available = new Dictionary<int, int>(CashInventory);
            foreach (var kv in CurrentTransaction.Inserted)
            {
                if (!available.ContainsKey(kv.Key)) available[kv.Key] = 0;
                available[kv.Key] += kv.Value;
            }

            var changeBreakdown = MakeChange(change, available);
            if (changeBreakdown == null)
            {
                return (false, "Невозможно выдать точную сдачу. Отмена покупки.", null);
            }

            product.Quantity--;
            foreach (var kv in CurrentTransaction.Inserted)
            {
                if (!CashInventory.ContainsKey(kv.Key)) CashInventory[kv.Key] = 0;
                CashInventory[kv.Key] += kv.Value;
            }

            foreach (var kv in changeBreakdown)
            {
                CashInventory[kv.Key] -= kv.Value;
                if (CashInventory[kv.Key] < 0) CashInventory[kv.Key] = 0;
            }
            CollectedKops += product.PriceKops;
            CurrentTransaction.Clear();
            return (true, $"Выдан товар: {product.Name}. Сдача: {Fmt(change)}.", changeBreakdown);
        }

        public Dictionary<int, int> CancelTransaction()
        {
            var ret = new Dictionary<int, int>(CurrentTransaction.Inserted);
            CurrentTransaction.Clear();
            return ret;
        }

        public static string Fmt(int kops)
        {
            return $"{kops / 100}.{(kops % 100):D2} руб.";
        }

        public string AdminInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Собранные средства (к которым имеет доступ админ): {Fmt(CollectedKops)}");
            sb.AppendLine("Касса (наличные в автомате):");
            foreach (var d in Denominations.All)
            {
                sb.AppendLine($"  {DenomStr(d)} x {CashInventory[d]}");
            }
            return sb.ToString();
        }

        public static string DenomStr(int kops)
        {
            if (kops >= 100)
                return $"{kops / 100} руб.";
            else
                return $"{kops} коп.";
        }

        public int CollectFunds()
        {
            int taken = CollectedKops;
            CollectedKops = 0;
            return taken;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var vm = CreateSampleMachine();
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("=== ВЕНДИНГОВЫЙ АВТОМАТ (демо) ===");

            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("1) Показать товары");
                Console.WriteLine("2) Вставить купюру/монету");
                Console.WriteLine("3) Выбрать товар (купить)");
                Console.WriteLine("4) Отменить операцию (вернуть вставленные деньги)");
                Console.WriteLine("5) Войти в админ-режим");
                Console.WriteLine("0) Выход");
                Console.Write("Выбери действие: ");
                var key = Console.ReadLine();
                Console.WriteLine();

                switch (key)
                {
                    case "1":
                        ShowProducts(vm);
                        Console.WriteLine($"Внесено: {VendingMachine.Fmt(vm.CurrentTransaction.TotalKops())}");
                        break;
                    case "2":
                        InsertMoney(vm);
                        break;
                    case "3":
                        Purchase(vm);
                        break;
                    case "4":
                        var ret = vm.CancelTransaction();
                        if (ret.Count == 0) Console.WriteLine("Ничего не было вставлено.");
                        else
                        {
                            Console.WriteLine("Возвращены монеты/купюры:");
                            foreach (var kv in ret.OrderByDescending(k => k.Key))
                                Console.WriteLine($"  {VendingMachine.DenomStr(kv.Key)} x {kv.Value}");
                        }
                        break;
                    case "5":
                        AdminMenu(vm);
                        break;
                    case "0":
                        Console.WriteLine("Выход. До свидания!");
                        return;
                    default:
                        Console.WriteLine("Неверная команда.");
                        break;
                }
            }
        }

        static VendingMachine CreateSampleMachine()
        {
            var vm = new VendingMachine();
            vm.AddOrUpdateProduct(new Product("A1", "Шоколадка", 45, 10));
            vm.AddOrUpdateProduct(new Product("A2", "Чипсы", 60, 8));
            vm.AddOrUpdateProduct(new Product("B1", "Вода 0.5л", 35, 20));
            vm.AddOrUpdateProduct(new Product("B2", "Кофе горячий", 80, 10));
            vm.AddOrUpdateProduct(new Product("C1", "Сэндвич", 95, 5));

            vm.AddCash(100, 20);
            vm.AddCash(200, 10);
            vm.AddCash(500, 10);
            vm.AddCash(1000, 10);
            vm.AddCash(10000, 5);
            vm.AddCash(50000, 2);
            vm.AddCash(500000, 1);
            return vm;
        }

        static void ShowProducts(VendingMachine vm)
        {
            Console.WriteLine("Код | Наименование | Цена | Кол-во");
            Console.WriteLine("-----------------------------------");
            foreach (var p in vm.Products.Values)
            {
                Console.WriteLine($"{p.Code.PadRight(3)} | {p.Name.PadRight(12)} | {VendingMachine.Fmt(p.PriceKops).PadRight(7)} | {p.Quantity}");
            }
        }

        static void InsertMoney(VendingMachine vm)
        {
            Console.WriteLine("Выберите номинал для вставки:");
            for (int i = 0; i < Denominations.All.Length; i++)
            {
                Console.WriteLine($"{i + 1}) {VendingMachine.DenomStr(Denominations.All[i])}");
            }
            Console.Write("Номер номинала или 0 для отмены: ");
            if (!int.TryParse(Console.ReadLine(), out int idx)) { Console.WriteLine("Неверный ввод."); return; }
            if (idx == 0) return;
            if (idx < 1 || idx > Denominations.All.Length) { Console.WriteLine("Неверный выбор."); return; }
            int denom = Denominations.All[idx - 1];
            vm.CurrentTransaction.Insert(denom);
            Console.WriteLine($"Вставлено {VendingMachine.DenomStr(denom)}. Сейчас внесено: {VendingMachine.Fmt(vm.CurrentTransaction.TotalKops())}");
        }

        static void Purchase(VendingMachine vm)
        {
            Console.Write("Введите код товара: ");
            var code = Console.ReadLine().Trim().ToUpper();
            var (ok, msg, change) = vm.TryPurchase(code);
            Console.WriteLine(msg);
            if (ok)
            {
                if (change != null && change.Count > 0)
                {
                    Console.WriteLine("Сдача выдана:");
                    foreach (var kv in change.OrderByDescending(k => k.Key))
                        Console.WriteLine($"  {VendingMachine.DenomStr(kv.Key)} x {kv.Value}");
                }
            }
        }

        static void AdminMenu(VendingMachine vm)
        {
            Console.Write("Введите админ-пароль: ");
            var pw = Console.ReadLine();
            if (pw != "admin123")
            {
                Console.WriteLine("Неверный пароль.");
                return;
            }
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("АДМИН-РЕЖИМ:");
                Console.WriteLine("1) Показать товары и остатки");
                Console.WriteLine("2) Добавить/пополнить товар");
                Console.WriteLine("3) Показать кассу и собрать средства");
                Console.WriteLine("4) Пополнить кассу (добавить купюры/монеты в автомат)");
                Console.WriteLine("0) Выйти из админ-режима");
                Console.Write("Выбери: ");
                var c = Console.ReadLine();
                Console.WriteLine();
                if (c == "1")
                {
                    ShowProducts(vm);
                }
                else if (c == "2")
                {
                    Console.Write("Код товара: ");
                    var code = Console.ReadLine().Trim().ToUpper();
                    Console.Write("Название: ");
                    var name = Console.ReadLine().Trim();
                    Console.Write("Цена (рубли целые): ");
                    if (!int.TryParse(Console.ReadLine(), out int price)) { Console.WriteLine("Неверно."); continue; }
                    Console.Write("Количество: ");
                    if (!int.TryParse(Console.ReadLine(), out int qty)) { Console.WriteLine("Неверно."); continue; }
                    var prod = new Product(code, name, price, qty);
                    vm.AddOrUpdateProduct(prod);
                    Console.WriteLine("Товар добавлен/пополнен.");
                }
                else if (c == "3")
                {
                    Console.WriteLine(vm.AdminInfo());
                    Console.Write("Собрать собранные средства? (y/n): ");
                    var ans = Console.ReadLine().Trim().ToLower();
                    if (ans == "y")
                    {
                        int taken = vm.CollectFunds();
                        Console.WriteLine($"Админ забрал {VendingMachine.Fmt(taken)}.");
                    }
                }
                else if (c == "4")
                {
                    Console.WriteLine("Выберите номинал для добавления:");
                    for (int i = 0; i < Denominations.All.Length; i++)
                        Console.WriteLine($"{i + 1}) {VendingMachine.DenomStr(Denominations.All[i])}");
                    Console.Write("Номер: ");
                    if (!int.TryParse(Console.ReadLine(), out int idx)) { Console.WriteLine("Неверный ввод."); continue; }
                    if (idx < 1 || idx > Denominations.All.Length) { Console.WriteLine("Неверный выбор."); continue; }
                    int denom = Denominations.All[idx - 1];
                    Console.Write("Количество штук для добавления: ");
                    if (!int.TryParse(Console.ReadLine(), out int cnt)) { Console.WriteLine("Неверно."); continue; }
                    vm.AddCash(denom, cnt);
                    Console.WriteLine("Касса пополнена.");
                }
                else if (c == "0") break;
                else Console.WriteLine("Неверная команда.");
            }
        }
    }
}
