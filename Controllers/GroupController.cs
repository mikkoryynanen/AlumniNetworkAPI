﻿using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using AlumniNetworkAPI.Models.Domain;
using AlumniNetworkAPI.Models.DTO.Group;
using AlumniNetworkAPI.Services;
using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace AlumniNetworkAPI.Controllers
{
    [ApiController]
    [Route("api/v1/group")]
    [Produces(MediaTypeNames.Application.Json)]
    [Consumes(MediaTypeNames.Application.Json)]
    [ApiConventionType(typeof(DefaultApiConventions))]
    public class GroupController : ControllerBase
    {
        private readonly IMapper _mapper;
        private readonly IGroupService _groupService;
        private readonly IUserService _userService;

        public GroupController(IMapper mapper, IGroupService groupService, IUserService userService)
        {
            _mapper = mapper;
            _groupService = groupService;
            _userService = userService;
        }

        /// <summary>
        /// Returns a list of groups that the user has access to. 
        /// </summary>
        /// <returns></returns>
        [Authorize]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<GroupReadDTO>>> GetUserGroups()
        {
            // extract subject from token and find corresponding user
            string keycloakId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            User user = await _userService.FindUserByKeycloakIdAsync(keycloakId);

            return _mapper.Map<List<GroupReadDTO>>(await _groupService.GetUserGroupsAsync(user));
        }

        /// <summary>
        /// Returns the group corresponding to the provided id.
        /// </summary>
        /// <param name="id">Id of the group</param>
        /// <returns></returns>
        [Authorize]
        [HttpGet("{id}")]
        public async Task<ActionResult<GroupReadDTO>> GetGroup(int id)
        {
            // extract subject from token and find corresponding user
            string keycloakId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            User user = await _userService.FindUserByKeycloakIdAsync(keycloakId);

            Group group = await _groupService.GetSpecificGroupAsync(id);

            if (group == null)
            {
                return NotFound();
            }

            // check if the requesting user has access to the group
            if (!_groupService.UserHasGroupAccess(group, user))
            {
                // return 403 Forbidden
                return StatusCode(403);
            }
            
            return _mapper.Map<GroupReadDTO>(group);
        }

        /// <summary>
        /// Create a new group.
        /// </summary>
        /// <param name="dtoGroup">New group object to be created</param>
        /// <returns></returns>
        [Authorize]
        [HttpPost]
        public async Task<ActionResult<Group>> PostGroup(GroupCreateDTO dtoGroup)
        {
            // extract subject from token and find corresponding user
            string keycloakId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            User user = await _userService.FindUserByKeycloakIdAsync(keycloakId);

            Group domainGroup = _mapper.Map<Group>(dtoGroup);

            // add the group to the database
            domainGroup = await _groupService.AddGroupAsync(domainGroup);
            // add the requesting user as the first member of the group
            await _groupService.JoinGroupAsync(domainGroup, user);

            return CreatedAtAction("GetGroup",
                new { id = domainGroup.Id },
                _mapper.Map<GroupReadDTO>(domainGroup));
        }

        /// <summary>
        /// Create a new group membership record.
        /// </summary>
        /// <param name="id">Id of the group</param>
        /// <param name="userId">Optional id of the joining user in request body</param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("{id}/join")]
        public async Task<IActionResult> JoinGroup(int id, [FromBody] int userId = default)
        {
            // extract subject from token and find corresponding user
            string keycloakId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            User requestingUser = await _userService.FindUserByKeycloakIdAsync(keycloakId);
            User joiningUser = new User();

            //int requestingUserId = requestingUser.Id;

            // check request body for user id, use requesting user's id otherwise
            if (userId == default)
            {
                joiningUser = requestingUser;
            }
            // user id provided in request body
            else
            {
                if (!await _userService.UserExistsAsync(userId))
                {
                    return BadRequest("Invalid user id provided.");
                }
                joiningUser = await _userService.GetInfoAsync(userId);
            }

            // invalid group id
            if (!_groupService.GroupExists(id))
            {
                return NotFound();
            }

            Group group = await _groupService.GetSpecificGroupAsync(id);

            // check if the requesting user is not a member of the group
            if (!_groupService.UserHasGroupAccess(group, requestingUser))
            {
                // 403 Forbidden
                return StatusCode(403);
            }
            else
            {
                // add the specified user to the group
                await _groupService.JoinGroupAsync(group, joiningUser);
            }

            return NoContent();
        }

    }
}
