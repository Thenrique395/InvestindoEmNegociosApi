// Refatoração do ProfileController.cs seguindo o padrão Clean Architecture
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace InvestindoEmNegociosApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _profileService;

        public ProfileController(IProfileService profileService)
        {
            _profileService = profileService;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetProfile(int id)
        {
            var profile = await _profileService.GetProfileByIdAsync(id);
            if (profile == null)
                return NotFound();

            return Ok(profile);
        }

        [HttpPost]
        public async Task<IActionResult> CreateProfile([FromBody] ProfileDto profileDto)
        {
            var createdProfile = await _profileService.CreateProfileAsync(profileDto);
            return CreatedAtAction(nameof(GetProfile), new { id = createdProfile.Id }, createdProfile);
        }

        // Outros métodos como UpdateProfile, DeleteProfile, etc., também devem ser refatorados de forma semelhante.
    }
}