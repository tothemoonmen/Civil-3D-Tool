using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//Use https://help.autodesk.com/view/OARX/2024/ENU/?guid=GUID-2363CE7C-AC2B-4CAC-AE5D-F77B386132D7 to set up AutoDesk STuff

namespace Hello_World
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello WOrld!");
<<<<<<< Updated upstream
=======
            Console.WriteLine("Please enter your name:");
            Console.WriteLine("Hello " + Name());
            int num = int.Parse(Console.ReadLine());
            if (IsEven(num))
            {
                Console.WriteLine("The inputed number is even");
            }
            else
            {
                Console.WriteLine("The inputed number is odd");
            }
        }

        static string Name()
        {
            String name = Console.ReadLine();
            return name;
        }
        
        static Boolean IsEven(int innumber)
        {
            if (innumber % 2 == 0)
            {
                return true;
            }
            else
            {
                return false;
            }

>>>>>>> Stashed changes
        }
    }
}
