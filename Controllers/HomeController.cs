using Microsoft.AspNetCore.Mvc;

namespace UploadFile.Controllers
{
    [Route("/")]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
