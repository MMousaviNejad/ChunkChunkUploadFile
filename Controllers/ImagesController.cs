using Microsoft.AspNetCore.Mvc;
using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;

namespace UploadFile.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImagesController : ControllerBase
    {
        private readonly IHostingEnvironment _environment;

        public ImagesController(IHostingEnvironment environment)
        {
            _environment = environment;
        }

        [HttpPost("UploadChunk")]
        public async Task<IActionResult> UploadChunk(string apiKey, [FromForm] IFormFile chunk, [FromForm] string fileName, [FromForm] int chunkIndex, [FromForm] int totalChunks)
        {
            Response.Headers.Add("Content-Security-Policy", "default-src 'self'; img-src 'self'; script-src 'self'; style-src 'self';");

            if (apiKey != "mysecretkey")
                return Unauthorized("Invalid API Key");

            if (chunk == null || chunk.Length == 0 || string.IsNullOrEmpty(fileName))
                return BadRequest("Invalid chunk data.");

            if (fileName.Contains('.') && !Guid.TryParse(fileName.Substring(0, fileName.LastIndexOf('.')), out _))
            {
                fileName = Guid.NewGuid().ToString() + fileName.Substring(fileName.LastIndexOf('.'));
            }

            string tempFolder = Path.Combine(_environment.WebRootPath, "TempUploads", fileName);

            try
            {

                if (chunkIndex == 0)
                {
                    byte[] buffer = new byte[8];
                    using (var stream = chunk.OpenReadStream())
                    {
                        await stream.ReadAsync(buffer, 0, buffer.Length);
                    }

                    if (!IsImage(buffer))
                        return BadRequest("Invalid file type. Only image files are allowed.");
                }

                if (!Directory.Exists(tempFolder))
                    Directory.CreateDirectory(tempFolder);

                string chunkPath = Path.Combine(tempFolder, $"{chunkIndex}.chunk");

                using (var stream = new FileStream(chunkPath, FileMode.Create, FileAccess.Write))
                {
                    await chunk.CopyToAsync(stream);
                }

                return Ok(new { success = true, chunkIndex, fileName });
            }
            catch (Exception ex)
            {
                if (Directory.Exists(tempFolder))
                    Directory.Delete(tempFolder, true);
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("Upload")]
        public async Task<IActionResult> Upload(string apiKey, [FromForm] string fileName)
        {

            if (apiKey != "mysecretkey")
                return Unauthorized("Invalid API Key");

            string tempFolder = Path.Combine(_environment.WebRootPath, "TempUploads", fileName);

            if (!Directory.Exists(tempFolder))
                return NotFound("File not found!");

            string finalPath = Path.Combine(_environment.WebRootPath, "Uploads", fileName);
            if (!Directory.Exists(Path.GetDirectoryName(finalPath)))
                Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);

            try
            {
                using (var finalStream = new FileStream(finalPath, FileMode.Create, FileAccess.Write))
                {
                    for (int i = 0; i < Directory.GetFiles(tempFolder).Length; i++)
                    {
                        string currentChunkPath = Path.Combine(tempFolder, $"{i}.chunk");
                        using (var chunkStream = new FileStream(currentChunkPath, FileMode.Open, FileAccess.Read))
                        {
                            await chunkStream.CopyToAsync(finalStream);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (System.IO.File.Exists(finalPath))
                    System.IO.File.Delete(finalPath);
                return StatusCode(500, ex.Message);
            }

            if (Directory.Exists(tempFolder))
                Directory.Delete(tempFolder, true);
            return Ok(new { success = true });

        }

        private bool IsImage(byte[] headerBytes)
        {
            var imageSignatures = new Dictionary<string, byte[]>
            {
                { "jpeg", new byte[] { 0xFF, 0xD8, 0xFF } },
                { "png", new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
                { "gif", new byte[] { 0x47, 0x49, 0x46, 0x38 } },
                { "bmp", new byte[] { 0x42, 0x4D } }
            };

            foreach (var signature in imageSignatures.Values)
            {
                if (headerBytes.Take(signature.Length).SequenceEqual(signature))
                    return true;
            }

            return false;
        }
    }
}
