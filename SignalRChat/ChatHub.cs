using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.AspNet.SignalR;
using SignalRChat.Common;
using System.Diagnostics;

namespace SignalRChat
{
    public class ChatHub : Hub
    {
        #region Data Members
        // Consultores
        static List<ConsultorDetail> ConsultorsUsers = new List<ConsultorDetail>();

        // Medicos que estan en la cola de espera
        static List<UserDetail> QueueUsers = new List<UserDetail>();

        // Medicos asignados a un consultor
        static List<UserDetail> TalkingUsers = new List<UserDetail>();

        static List<MessageDetail> CurrentMessage = new List<MessageDetail>();

        #endregion

        #region Usuarios

        public Boolean checkConsultor(string usname)
        {
            // Comprobar array de consultores
            return (usname.Equals("paco") || usname.Equals("manolo") || usname.Equals("pepito")) ? true : false;
        }

        public String[] GetNoConsultores()
        {
            List<String> idsCola = QueueUsers.Select(x => x.ConnectionId).ToList();
            List<String> ids = idsCola.Concat(TalkingUsers.Select(x => x.ConnectionId)).ToList();

            return ids.ToArray();
        }
        #endregion      


        #region Methods

        public void Connect(string userName)
        {
            var id = Context.ConnectionId;

            if (QueueUsers.FindIndex(x => x.ConnectionId == id) == -1
                && TalkingUsers.FindIndex(x => x.ConnectionId == id) == -1
                && ConsultorsUsers.FindIndex(x => x.ConnectionId == id) == -1)
            {               
                // Si el usuario es un consultor, se le devuelve la lista de no consultores que no han sido asignados
                if (checkConsultor(userName))
                {

                    ConsultorDetail cons = new ConsultorDetail { ConnectionId = id, UserName = userName, IsConsultor = checkConsultor(userName), IsAsignado = false,UsuariosAsignados = new List<UserDetail>()};
                    // Cogemos la lista de no consultores  junto con el nuevo consultor para crear el filtro de los usuarios
                    // a los que no se le tiene que actualizar la lista de usuarios conectados 
                    var conss = GetNoConsultores().ToList();
                    conss.Add(id);

                    // Cargamos los datos de los consultores que ya estaban conectados
                    // en el nuevo consultor
                    Clients.Caller.addAllConsultorsInfo(ConsultorsUsers);

                    // Incorporamos a la lista de los consultores activos el nuevo
                    ConsultorsUsers.Add(cons);

                    // Actualizamos al resto de consultores la lista de estos
                    Clients.AllExcept(conss.ToArray()).addOtherConsultor(id, userName, new List<Object>());

                    // Muesta el listado de los usuarios (no consultores y no asignados) al nuevo consultor
                    Clients.Caller.onConnected(id, userName, QueueUsers, CurrentMessage);
                } 


                // Si el usuario no es un consultor, se le avisará a todos los excepto a los que no sean consultores
                else
                {
                    UserDetail usuario = new UserDetail { ConnectionId = id, UserName = userName, IsConsultor = checkConsultor(userName), IsAsignado = false };

                    QueueUsers.Add(usuario);
                    Clients.AllExcept(GetNoConsultores()).onNewUserConnected(id, userName);
                }
            }

        }

      

        public void SendMessageToAll(string userName, string message)
        {
            // store last 100 messages in cache
            AddMessageinCache(userName, message);

            // Broad cast message 
            Clients.AllExcept(GetNoConsultores()).messageReceived(userName, message);
        }

        public void AsignaUsuarioAConsultorActual(string usuario)
        {
            string consultorID = Context.ConnectionId;
            ConsultorDetail cons = ConsultorsUsers.Where(x => x.ConnectionId == consultorID).FirstOrDefault();
            UserDetail usu = QueueUsers.Where(x => x.UserName == usuario).FirstOrDefault();

            if (cons != null && usu != null)
            {
                cons.UsuariosAsignados.Add(usu);

                TalkingUsers.Add(usu);
                QueueUsers.Remove(usu);

                Clients.AllExcept(GetNoConsultores()).onUserDisconnected(usu.ConnectionId, usu.UserName);

                Clients.Caller.addUserAssigned(usu.ConnectionId, usu.UserName);
            }

        }

        public void SendPrivateMessage(string toUserId, string message)
        {

            string fromUserId = Context.ConnectionId;

            var usu1 = (UserDetail) ConsultorsUsers.FirstOrDefault(x => x.ConnectionId == fromUserId);
            var usu2 = (UserDetail) ConsultorsUsers.FirstOrDefault(x => x.ConnectionId == toUserId);

            if(usu1 == null){
                usu1 = TalkingUsers.FirstOrDefault(x => x.ConnectionId == fromUserId);
            }else{
                usu2 = TalkingUsers.FirstOrDefault(x => x.ConnectionId == toUserId);
            }

            var toUser = usu2;
            var fromUser = usu1;

            if (toUser != null && fromUser!=null)
            {
                // send to 
                Clients.Client(toUserId).sendPrivateMessage(fromUserId, fromUser.UserName, message); 

                // send to caller user
                Clients.Caller.sendPrivateMessage(toUserId, fromUser.UserName, message); 
            }

        }

        public override System.Threading.Tasks.Task OnDisconnected()
        {
            var item = QueueUsers.FirstOrDefault(x => x.ConnectionId == Context.ConnectionId);

            if (item != null)
            {
                QueueUsers.Remove(item);

                var id = Context.ConnectionId;
                Clients.AllExcept(GetNoConsultores()).onUserDisconnected(id, item.UserName);
            }

            return base.OnDisconnected();
        }

     
        #endregion

        #region private Messages

        private void AddMessageinCache(string userName, string message)
        {
            CurrentMessage.Add(new MessageDetail { UserName = userName, Message = message });

            if (CurrentMessage.Count > 100)
                CurrentMessage.RemoveAt(0);
        }

        #endregion
    }

}