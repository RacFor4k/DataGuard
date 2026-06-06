using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Client.Engine.Services
{
    internal class AuthenticationService
    {
        //TODO: Релизовать получение passKey
        public AuthenticationService(string hostUrl, string passKey, string pinCode)
        {
        }

        public async Task<bool> AuthenticateAsync()
        {
            return true;
        }
    }
}
