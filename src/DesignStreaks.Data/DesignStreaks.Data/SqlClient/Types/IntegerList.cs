// *
// * DESIGNSTREAKS CONFIDENTIAL
// * __________________
// *
// *  Copyright © Design Streaks - 2010 - 2014
// *  All Rights Reserved.
// *
// * NOTICE:  All information contained herein is, and remains
// * the property of DesignStreaks and its suppliers, if any.
// * The intellectual and technical concepts contained
// * herein are proprietary to DesignStreaks and its suppliers and may
// * be covered by Australian, U.S. and Foreign Patents,
// * patents in process, and are protected by trade secret or copyright law.
// * Dissemination of this information or reproduction of this material
// * is strictly forbidden unless prior written permission is obtained
// * from DesignStreaks.

namespace DesignStreaks.Data.SqlClient.Types
{
    using System.Collections.Generic;
    using System.Data;
    using Microsoft.SqlServer.Server;

    /// <summary>Class used to pass a list of integer values through a Sql Server Table Valued Parameter.</summary>
    public class IntegerList : List<int>, IEnumerable<SqlDataRecord>, IEnumerable<int>
    {
        /// <summary>Returns an enumerator that iterates through a collection.</summary>
        /// <returns>An IEnumerator object that can be used to iterate through the collection.</returns>
        IEnumerator<SqlDataRecord> IEnumerable<SqlDataRecord>.GetEnumerator()
        {
            var sdr = new SqlDataRecord(
                     new SqlMetaData("Id", SqlDbType.Int));

            foreach (int item in this)
            {
                sdr.SetInt32(0, item);

                yield return sdr;
            }
        }

        /// <summary>Returns an enumerator that iterates through a collection.</summary>
        /// <returns>An IEnumerator object that can be used to iterate through the collection.</returns>
        IEnumerator<int> IEnumerable<int>.GetEnumerator()
        {
            foreach (int item in this)
            {
                yield return item;
            }
        }

        /// <summary>Returns an enumerator that iterates through a collection.</summary>
        /// <returns>An IEnumerator object that can be used to iterate through the collection.</returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}