using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reinforcement.Aspirant
{
    public static class ErrorAnalyse
    {
        private static int _inLogError;
        public static void Error(Exception ex=null, string message="", string cod="")
        {
            if (ex!=null)
            {
                AppErrors.LogError(ex);
            }
            
            if (Interlocked.CompareExchange(ref _inLogError, 1, 0) != 0) return;
            try
            {
                Error(message, cod);
            }
            catch
            {

            }
            finally
            {
                Interlocked.Exchange(ref _inLogError, 0);
            }
        }
        private static void Error(string message = "", string cod = "")
        {
            if(!string.IsNullOrEmpty(message)) {return; }

        }

        
    }
}
