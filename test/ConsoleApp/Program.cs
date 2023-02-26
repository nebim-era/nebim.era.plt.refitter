using Refit;
using System;
using System.Threading.Tasks;

namespace MyNamespace;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var client = RestService.For<Petstore.ISwaggerPetstore>("https://petstore3.swagger.io/api/v3");
        var pet = await client.GetPetById(2);

        Console.WriteLine($"Name: {pet.Name}");
        Console.WriteLine($"Category: {pet.Category.Name}");
        Console.WriteLine($"Status: {pet.Status}");
        Console.ReadLine();
    }
}