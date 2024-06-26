using System.Data;
using System.Security.Cryptography;
using Gladwyne.API.Data;
using Gladwyne.Helpers;
using Gladwyne.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Gladwyne.API.Controllers
{
    [Authorize] //This is something we use to tell the controller we want to authorize the user to access the controller.
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        //Constructor
        private readonly DataContextDapper _dapper;
        private readonly AuthHelper _authHelper;
        public AuthController(IConfiguration configuration)
        {
            _dapper = new DataContextDapper(configuration);
            _authHelper = new AuthHelper(configuration);
        }
        //We won't pull information out of here, except maybe a token renewal.
        //We want to register a user, and allow them to login.
        //In both cases the user will provide us with some information
        //Or we can compare the password they entered to the password they provided.

        [AllowAnonymous] //This is how we indicate to the controller that this endpoint is allowed to receive anonymous requests.
        [HttpPost("Register")]
        public IActionResult Register(UserForRegistrationDTO newUser)
        {
            //This can also be accomplished in the form.
            if(newUser.Password == newUser.PasswordConfirm)
            {
                //We need to check if there is a user with that email in our system already.
                string sqlCheckUserExists = $"Select Email From GladwyneSchema.Auth WHERE Email = '{newUser.Email}'";

                IEnumerable<string> existingUsers = _dapper.LoadData<string>(sqlCheckUserExists);
                if(existingUsers.Count() == 0)
                {
                    //We know the user doesn't exist.
                    //We know that there are two matching passwords.
                    //We will now generate the necessary data points to register our user. So password hash, password salt etc..

                    byte[] passwordSalt = new byte[128/8]; //This sets the size to 128 bytes.

                    using(RandomNumberGenerator randomNumber = RandomNumberGenerator.Create())
                    {
                        //'RandomNumberGenerator' generates a random number and passes it into the variable, 'randomNumber'

                        //variable password salt contains a random array.
                        // 1.)We are using the random number array with the user the password sent us (in newUser.Password and newUser.PasswordConfirm respectively)
                        //    To generate 'hashed' password which is more secure.
                        randomNumber.GetNonZeroBytes(passwordSalt);
                    }
                        byte[] passwordHash = _authHelper.GetPasswordHash(newUser.Password, passwordSalt);

                        //4.) We are creating our SQL Method.
                        string sqlAddAuthentication = $"INSERT INTO GladwyneSchema.Auth (Email, PasswordHash, PasswordSalt) VALUES ('{newUser.Email}', @PasswordHash, @PasswordSalt)";

                        //5.) We are creating a list of SQL Paramters. We are doing this so we can add to the list once we create those paramters.
                        //    we will then include that list of parameters with our sql call.
                        //    The below is a list of parameters.
                        List<SqlParameter> sqlParameters = new List<SqlParameter>();

                        //6.) We will now create two new paramters and then pass them into our list.
                        //6a.) We are generating our necessary types, and then passing those values into
                        //     the variable which is of the SqlParameter type, so it can be used within our query.
                        SqlParameter passwordSaltParameter = new SqlParameter("@PasswordSalt", SqlDbType.VarBinary);
                        passwordSaltParameter.Value = passwordSalt;
                        SqlParameter passwordHashParameter = new SqlParameter("@PasswordHash", SqlDbType.VarBinary);
                        passwordHashParameter.Value = passwordHash;

                        //7.) We will now add them to the list of parameters. That is the, 'sqlParameters' variable.
                        sqlParameters.Add(passwordSaltParameter);
                        sqlParameters.Add(passwordHashParameter);

                        // We are now ready to pass this into our DB.
                        if(_dapper.ExecuteSqlWithParameters(sqlAddAuthentication, sqlParameters))
                        {
                            string sqlAddUser = $"INSERT INTO GladwyneSchema.USERS(FirstName, Lastname, Email) VALUES ('{newUser.FirstName}', '{newUser.LastName}', '{newUser.Email}')";
                            if(_dapper.ExecuteSql(sqlAddUser))
                            {
                                return Ok();
                            }
                        }
                        throw new Exception("Failed to Register User");
                }
                throw new Exception("User With This Email Already Exists");
            }
            throw new Exception("Passwords Do Not Match");
        }
        [AllowAnonymous]
        [HttpPost("Login")]
        public IActionResult Login(UserForLoginDTO loginUser)
        {
            //We want to run a query that uses the email to get the Password Hash and Salt.
            string sqlForHashAndSalt = $"Select PasswordHash, PasswordSalt From GladwyneSchema.Auth WHERE Email = '{loginUser.Email}'";

            UserForLoginConfirmationDTO userForConfirmation = _dapper.LoadDataSingle<UserForLoginConfirmationDTO>(sqlForHashAndSalt);
            byte[] passwordHash = _authHelper.GetPasswordHash(loginUser.Password, userForConfirmation.PasswordSalt);

            for(int index = 0; index < passwordHash.Length; index ++)
            {
                if(passwordHash[index] != userForConfirmation.PasswordHash[index])
                {
                    return StatusCode(401, "Incorrect Password!");
                }
            }
            string getUserIdSql = $"SELECT * FROM GladwyneSchema.Users Where Email = '{loginUser.Email}'";
            int userId = _dapper.LoadDataSingle<int>(getUserIdSql);
            return Ok(new Dictionary<string, string> {
                {"token", _authHelper.CreateToken(userId)}
            });
        }

        [HttpGet("RefreshToken")]
        public IActionResult RefreshToken()
        {
            string userId = User.FindFirst("userId")?.Value + "";

            string userIdSql = $"SELECT UserId FROM GladwyneSchema.Users WHERE userId = '{userId}'";

            int userIdFromDB = _dapper.LoadDataSingle<int>(userIdSql);
            return Ok(new Dictionary<string, string> {
                {"token", _authHelper.CreateToken(userIdFromDB)}
            });
        }
    }
}

