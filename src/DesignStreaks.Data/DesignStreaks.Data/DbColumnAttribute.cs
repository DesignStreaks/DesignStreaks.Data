// *
// * DESIGNSTREAKS CONFIDENTIAL
// * __________________
// *
// *  Copyright © Design Streaks - 2010 - 2018
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

namespace DesignStreaks.Data
{
    using System;
    using System.ComponentModel;

    /// <summary>Property Attribute to set the Sql column order of class fields. This class cannot be inherited.</summary>
    /// <seealso cref="System.Attribute" />
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    [ImmutableObject(true)]
    public sealed class DbColumnAttribute : Attribute
    {
        /// <summary>The (zero-based) order of the field when converted to a Sql Type.</summary>
        /// <value>The order.</value>
        public int Order { get; private set; }

        /// <summary>Initializes a new instance of the <see cref="DbColumnAttribute"/> class.</summary>
        /// <param name="order">The field sequence order.</param>
        public DbColumnAttribute(int order)
        {
            this.Order = order;
        }
    }
}