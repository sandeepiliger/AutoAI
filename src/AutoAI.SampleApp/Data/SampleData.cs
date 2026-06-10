using AutoAI.SampleApp.Models;

namespace AutoAI.SampleApp.Data;

public static class SampleData
{
    public static IReadOnlyList<Order> Orders { get; } = new[]
    {
        new Order(1001, "Acme Corp",       2500.00m, "Shipped"),
        new Order(1002, "Globex",           780.50m, "Pending"),
        new Order(1003, "Initech",         1320.00m, "Shipped"),
        new Order(1004, "Umbrella Co",      499.99m, "Cancelled"),
        new Order(1005, "Stark Industries", 9100.00m, "Shipped"),
        new Order(1006, "Wayne Enterprises", 4250.75m, "Pending"),
        new Order(1007, "Hooli",            310.25m, "Shipped"),
        new Order(1008, "Pied Piper",       129.00m, "Pending"),
        new Order(1009, "Soylent Corp",    2210.40m, "Shipped"),
        new Order(1010, "Vandelay Industries", 875.00m, "Shipped"),
    };

    public static IReadOnlyList<Customer> Customers { get; } = new[]
    {
        new Customer(1, "Alice Johnson",  "Seattle"),
        new Customer(2, "Bob Smith",      "Austin"),
        new Customer(3, "Carol Davis",    "Denver"),
        new Customer(4, "David Lee",      "Chicago"),
        new Customer(5, "Emma Wilson",    "Boston"),
        new Customer(6, "Frank Miller",   "Portland"),
        new Customer(7, "Grace Chen",     "San Jose"),
        new Customer(8, "Henry Garcia",   "Miami"),
    };
}
