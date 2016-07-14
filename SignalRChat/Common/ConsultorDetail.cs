using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SignalRChat.Common
{
    public class ConsultorDetail : UserDetail
    {
        public List<String> UsuariosAsignados { get;  set; }
    }
}