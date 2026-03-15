using CloudCacheManager.Models;
using CloudCacheManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CloudCacheManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private readonly FileManagerService _fileService;

    public FilesController(FileManagerService fileService)
    {
        _fileService = fileService;
    }

    // Extracts the logged-in username from the JWT token claims
    private string CurrentUsername =>
        User.FindFirstValue(ClaimTypes.Name) ?? "unknown";

    [HttpGet("basepath")]
    [Authorize(Policy = Policies.CanDownload)]
    public IActionResult GetBasePath()
    {
        return Ok(new ApiResponse<string>
        {
            Success = true,
            Data = _fileService.BasePath  
        });
    }

    // GET: api/files/tree
    // All authenticated users (Admin, Manager, Viewer) can browse the tree.
    // Unauthenticated requests are blocked — nobody sees structure without login.
    [HttpGet("tree")]
    [Authorize(Policy = Policies.CanDownload)]
    public IActionResult GetTree([FromQuery] string? path = null)
    {
        try
        {
            var tree = _fileService.GetDirectoryTree(path);
            return Ok(new ApiResponse<FolderNode> { Success = true, Data = tree });
        }
        catch (DirectoryNotFoundException ex)
        {
            return NotFound(new ApiResponse<object> { Success = false, Message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object> { Success = false, Message = ex.Message });
        }
    }

    // GET: api/files/download?path=...
    // Requires authentication. Admin, Manager, Viewer can all download.
    [HttpGet("download")]
    [Authorize(Policy = Policies.CanDownload)]
    public IActionResult Download([FromQuery] string path)
    {
        try
        {
            var bytes = _fileService.DownloadFile(path);
            var fileName = System.IO.Path.GetFileName(path);
            var mimeType = fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? "application/json"
                : "application/octet-stream";
            return File(bytes, mimeType, fileName);
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new ApiResponse<object> { Success = false, Message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new ApiResponse<object> { Success = false, Message = ex.Message });
        }
    }

    // GET: api/files/read?path=...
    // ONLY Admin and Manager can read file content. Viewer gets 403.
    [HttpGet("read")]
    [Authorize(Policy = Policies.CanEdit)]
    public IActionResult ReadFile([FromQuery] string path)
    {
        try
        {
            var content = _fileService.ReadFileContent(path);
            return Ok(new ApiResponse<string> { Success = true, Data = content });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
        }
    }

    // PUT: api/files/edit
    // ONLY Admin and Manager can edit. Viewer gets 403.
    [HttpPut("edit")]
    [Authorize(Policy = Policies.CanEdit)]
    public IActionResult EditFile([FromBody] EditFileRequest request)
    {
        try
        {
            var backup = _fileService.EditFile(request.FilePath, request.Content, CurrentUsername, request.DisplayName);
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "File updated successfully",
                Backup = backup
            });
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new ApiResponse<object> { Success = false, Message = ex.Message });
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new ApiResponse<object> { Success = false, Message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object> { Success = false, Message = ex.Message });
        }
    }

    // POST: api/files/addkeyvalue
    // ONLY Admin and Manager can add key-value. Viewer gets 403.
    [HttpPost("addkeyvalue")]
    [Authorize(Policy = Policies.CanEdit)]
    public IActionResult AddKeyValue([FromBody] AddKeyValueRequest request)
    {
        try
        {
            var backup = _fileService.AddKeyValue(request.FilePath, request.Key, request.Value, CurrentUsername, request.DisplayName);
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = $"Key '{request.Key}' added successfully",
                Backup = backup
            });
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new ApiResponse<object> { Success = false, Message = ex.Message });
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new ApiResponse<object> { Success = false, Message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object> { Success = false, Message = ex.Message });
        }
    }
}


