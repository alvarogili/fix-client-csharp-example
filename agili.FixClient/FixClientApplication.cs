using System;
using QuickFix;

namespace agili.FixClient
{
    internal class FixClientApplication
    {
        public static void Main(string[] args)
        {
            try
            {
                QuickFix.SessionSettings settings = new QuickFix.SessionSettings("application.cfg");
                FixClient fixClient = new FixClient("user", "password");
                QuickFix.IMessageStoreFactory storeFactory = new QuickFix.FileStoreFactory(settings);
                QuickFix.ILogFactory logFactory = new QuickFix.ScreenLogFactory(settings);
                QuickFix.Transport.SocketInitiator initiator = new 
                    QuickFix.Transport.SocketInitiator(fixClient, storeFactory, settings, logFactory);
                initiator.Start();
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }
    }
}