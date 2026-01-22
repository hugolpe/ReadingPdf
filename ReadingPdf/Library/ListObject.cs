using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
//using ReadingPdf.Areas.Users.Models;
using ReadingPdf.Data;

namespace ReadingPdf.Library
{
    public class ListObject
    {
        //public LUsersRoles _usersRole;

        public IdentityError identityError;
        public ApplicationDbContext _context;
        public IWebHostEnvironment _environment;

        public RoleManager<IdentityRole> _roleManager;
        public UserManager<IdentityUser> _userManager;
        public SignInManager<IdentityUser> _signInManager;
        //public async Task<List<InputModelRegister>> GetTUsuariosAsync(string valor, int id)

        //public async Task<List<InputModelRegister>> GetTUsuariosAsync(int id, string valor)

        //{
        //    List<TUsers> listUser;
        //    List<SelectListItem> _listRoles;
        //    List<InputModelRegister> userList = new List<InputModelRegister>();
        //    if (valor == null && id.Equals(0))
        //    {
        //        listUser = _context.TUsers.ToList();
        //    }
        //    else
        //    {
        //        if (id.Equals(0))
        //        {
        //            listUser = _context.TUsers.Where(u => u.NID.StartsWith(valor) ||
        //                                                                                     u.Name.StartsWith(valor) ||
        //                                                                                     u.LastName.StartsWith(valor) ||
        //                                                                                     u.Email.StartsWith(valor)).ToList();
        //        }
        //        else
        //        {
        //            listUser = _context.TUsers.Where(u => u.ID.Equals(id)).ToList();
        //        }
        //    }
        //    if (!listUser.Count.Equals(0))
        //    {
        //        foreach (var item in listUser)
        //        {
        //            _listRoles = await _usersRole.getRole(_userManager, _roleManager, item.IdUser);
        //            var user = _context.Users.Where(u => u.Id.Equals(item.IdUser)).ToList().Last();
        //            userList.Add(new InputModelRegister
        //            {
        //                Id = item.ID,
        //                ID = item.IdUser,
        //                NID = item.NID,
        //                Name = item.Name,
        //                LastName = item.LastName,
        //                Email = item.Email,
        //                Role = _listRoles[0].Text,
        //                Image = item.Image,
        //                IdentityUser = user
        //            });
        //            _listRoles.Clear();
        //        }
        //    }
        //    return userList;
        //}



    }
}

