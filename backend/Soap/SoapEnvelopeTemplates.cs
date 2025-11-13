using System.Security;

namespace Mekashron.Soap
{
    public static class SoapEnvelopeTemplates
    {
        private const string NsSoap11 = "http://schemas.xmlsoap.org/soap/envelope/";
        private const string NsSoap12 = "http://www.w3.org/2003/05/soap-envelope";
        private const string NsIcu    = "urn:ICUTech.Intf-IICUTech";

        private static string E(string s) => SecurityElement.Escape(s) ?? string.Empty;


        public static string Login_v1(string username, string password) => $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""{NsSoap11}"">
  <soap:Body>
    <Login xmlns=""{NsIcu}"">
      <username>{E(username)}</username>
      <password>{E(password)}</password>
    </Login>
  </soap:Body>
</soap:Envelope>";

        public static string Login_v2(string username, string password) => $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""{NsSoap11}"">
  <soap:Body>
    <Login xmlns=""{NsIcu}"">
      <ol_Username>{E(username)}</ol_Username>
      <ol_Password>{E(password)}</ol_Password>
    </Login>
  </soap:Body>
</soap:Envelope>";


        public static string Register_v1(
            string username, string password, string email,
            string firstName, string lastName, string mobile) => $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""{NsSoap11}"">
  <soap:Body>
    <RegisterNewCustomer xmlns=""{NsIcu}"">
      <username>{E(username)}</username>
      <password>{E(password)}</password>
      <email>{E(email)}</email>
      <firstName>{E(firstName)}</firstName>
      <lastName>{E(lastName)}</lastName>
      <mobile>{E(mobile)}</mobile>
    </RegisterNewCustomer>
  </soap:Body>
</soap:Envelope>";

        public static string Register_v2(
            string username, string password, string email,
            string firstName, string lastName, string mobile) => $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""{NsSoap11}"">
  <soap:Body>
    <RegisterNewCustomer xmlns=""{NsIcu}"">
      <ol_Username>{E(username)}</ol_Username>
      <ol_Password>{E(password)}</ol_Password>
      <ol_Email>{E(email)}</ol_Email>
      <ol_FirstName>{E(firstName)}</ol_FirstName>
      <ol_LastName>{E(lastName)}</ol_LastName>
      <ol_Mobile>{E(mobile)}</ol_Mobile>
    </RegisterNewCustomer>
  </soap:Body>
</soap:Envelope>";

        public static string ToSoap12(string soap11)
            => soap11.Replace(NsSoap11, NsSoap12);
    }
}
