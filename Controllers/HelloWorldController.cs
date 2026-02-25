using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Pehlione.Controllers
{
    public class HelloWorldController
    {
         // 
    // GET: /HelloWorld/
    public string Index()
    {
        return "This is my default action...";
    }
    // 
    // GET: /HelloWorld/Welcome/ 
    public string Welcome()
    {
        return "This is the Welcome action method...";
    }
    }
}