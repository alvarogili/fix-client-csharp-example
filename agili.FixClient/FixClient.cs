using System;
using System.Collections.Generic;
using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX44;
using Message = QuickFix.Message;

namespace agili.FixClient
{
    public class FixClient : MessageCracker, IApplication
    {
        private string _user;
        private string password;
        private bool _connected = false;
        private Dictionary<String, Decimal> priceBySymbol = new Dictionary<String, Decimal>();

        //attributes for text purpose
        private string _lastTextMessage = "";
        private QuickFix.FIX50.MarketDataSnapshotFullRefresh _lastMarketDataSnapshotFullRefresh = null;

        public FixClient(string user, string password)
        {
            this._user = user;
            this.password = password;
        }

        public void ToAdmin(Message message, SessionID sessionId)
        {
            if (message.Header.GetString(Tags.MsgType).Equals(MsgType.LOGON))
            {
                message.SetField(new Username(_user));
                message.SetField(new Password(password));
            }
        }

        public void FromAdmin(Message message, SessionID sessionId)
        {
            Console.WriteLine("fromAdmin");
            FromApp(message, sessionId);
        }

        public void ToApp(Message message, SessionID sessionId)
        {
            Console.WriteLine("ToApp");
        }

        public void FromApp(Message message, SessionID sessionId)
        {
            try
            {
                Console.WriteLine("Message received. Type: " +
                                  message.Header.GetString(Tags.MsgType) +
                                  ". Details: "+ message.ToString());
                Crack(message, sessionId);
            }
            catch (UnsupportedMessageType unsupportedMessageType)
            {
                Console.WriteLine(unsupportedMessageType.ToString());
            }
            catch (IncorrectTagValue incorrectTagValue)
            {
                Console.WriteLine(incorrectTagValue.ToString());
            }
        }

        #region MessageCracker handlers
        public void OnMessage(QuickFix.FIX50.ExecutionReport message, SessionID sessionId)
        {
            if (EsExecutionReportNew(message))
            {
                throw new Exception("The order is old");
            }
        }

        public void OnMessage(QuickFix.FIX50.MarketDataRequestReject message, SessionID sessionId)
        {
            Console.WriteLine("MarketDataRequestReject");
        }

        public void OnMessage(QuickFix.FIX50.MarketDataSnapshotFullRefresh message, SessionID sessionId)
        {
            _lastMarketDataSnapshotFullRefresh = message;
            var noMdEntries = new
                MarketDataSnapshotFullRefresh.NoMDEntriesGroup();
            for (var i = 0; i < message.GetGroupTags().Count; i++)
            {
                var mdEntryType = new MDEntryType();
                var mdEntryPx = new MDEntryPx();
                var mdEntrySize = new MDEntrySize();
                message.GetGroup(i + 1, noMdEntries);
                noMdEntries.Get(mdEntryType);
                noMdEntries.Get(mdEntryPx);
                noMdEntries.Get(mdEntrySize);
                //TODO store
                priceBySymbol["EUR/USD"] = mdEntryPx.getValue();
            }
        }

        public void OnMessage(QuickFix.FIX50.MarketDataIncrementalRefresh message, SessionID sessionId)
        {
            var noMdEntries = new MarketDataIncrementalRefresh.NoMDEntriesGroup();
            for (var i = 0; i < message.GetGroupTags().Count; i++)
            {
                var mdEntryType = new MDEntryType();
                var mdEntryPx = new MDEntryPx();
                var mdEntrySize = new MDEntrySize();
                message.GetGroup(i + 1, noMdEntries);
                noMdEntries.Get(mdEntryType);
                noMdEntries.Get(mdEntryPx);
                noMdEntries.Get(mdEntrySize);
                //TODO store
                priceBySymbol["EUR/USD"] = mdEntryPx.getValue();
            }
        }
        
        public void OnMessage(Logon message, SessionID sessionId)
        {
        }

        public void OnMessage(RejectLogon message, SessionID sessionId)
        {
            _lastTextMessage = message.Message;
        }

        public void OnMessage(Logout message, SessionID sessionId)
        {
            _lastTextMessage = message.Text.ToString();
        }
        #endregion

        private static bool EsExecutionReportNew(QuickFix.FIX50.ExecutionReport executionReport)
        {
            return executionReport.ExecType.getValue() == ExecType.NEW;
        }

        public void OnCreate(SessionID sessionId)
        {
        }

        public void OnLogout(SessionID sessionId)
        {
            Console.WriteLine("OnLogout");
        }

        public void OnLogon(SessionID sessionId)
        {
            Console.WriteLine("Login with server successfully");
            this._connected = true;
            SendSubscriptions(sessionId);
        }

        private void SendSubscriptions(SessionID sessionId)
        {
            //basado en pag 35 de SBAFIX_MD_1_18_9_MAR2020_preliminar.pdf
            //ver notas pag 41
            var marketDataRequest = new MarketDataRequest();

            //session ID
            marketDataRequest.Set(new MDReqID(sessionId.ToString()));

            //Indica que tipo de respuesta se está esperando. Valores válidos:
            //0:Captura, 1:Captura+actualizaciones (subscripción) no
            //soportada, 2:Anular subscripción (no soportada para DMA).
            marketDataRequest.Set(new SubscriptionRequestType('0'));

            //Profundidad del Mercado tanto para capturas de libro, como
            //actualizaciones incrementales.
            //Para DMA no esta soportado, siempre se informan 5 niveles
            marketDataRequest.Set(new MarketDepth(1));

            //esta etiqueta se usa para describir el tipo de actualización de
            //Market Data y ByMA requiere el valor 1 en este campo.
            marketDataRequest.Set(new MDUpdateType(1));

            //Especifica si las entradas tienen o no que ser agregadas.
            marketDataRequest.Set(new AggregatedBook(false));

            // MDEntryType debe ser el primer campo en este grupo de
            //repetición. Se trata de un listado detallando la información
            //(MarketDataEntries) que la firma solicitante está interesada en
            //recibir
            MarketDataRequest.NoMDEntryTypesGroup entries = new MarketDataRequest.NoMDEntryTypesGroup();
            entries.Set(new MDEntryType(MDEntryType.BID));
            marketDataRequest.AddGroup(entries);

            //Especifica la cantidad de símbolos repetidos en el grupo.
            marketDataRequest.Set(new NoRelatedSym(1));
            MarketDataRequest.NoRelatedSymGroup symbols = new MarketDataRequest.NoRelatedSymGroup();
            symbols.Set(new Symbol("EUR/USD"));
            marketDataRequest.AddGroup(symbols);

            //Representación “humana” del título. En caso de no existir un
            //símbolo para el intrumento, puede asignarse el valor del
            //SecurityID. Solo usar “[N/A]” cuando se está solicitando
            //información por producto (Product = 7).
            //Ver “Instrumentos usados para informar datos estadisticos” para
            //suscribir mensajes de estadísticas
            Session.SendToTarget(marketDataRequest, sessionId);
            /*
            Luego de una subscripción correcta se recibe un MarketDataSnapshotFullRefresh (MsgType = W)
            y posteriormente se van recibiendo MarketDataIncrementalRefresh (MsgType = X)
             */
        }
    }
}