// Copyright 2007 - Ricardo Stuven (rstuven@gmail.com)
//
// This file is part of NHibernate.Spatial.
// NHibernate.Spatial is free software; you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// NHibernate.Spatial is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.

// You should have received a copy of the GNU Lesser General Public License
// along with NHibernate.Spatial; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

using GeoAPI.Geometries;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using NHibernate.Engine;
using NHibernate.SqlTypes;
using NHibernate.Type;

namespace NHibernate.Spatial.Type
{
    [Serializable]
    public class MsSql2008GeometryType : GeometryTypeBase<byte[]>
    {
        private static readonly NullableType GeometryType = new CustomGeometryType();

        /// <summary>
        /// Initializes a new instance of the <see cref="MsSql2008GeometryType"/> class.
        /// </summary>
        public MsSql2008GeometryType()
            : base(GeometryType)
        {
        }

        /// <summary>
        /// Converts from GeoAPI geometry type to database geometry type.
        /// </summary>
        /// <param name="value">The GeoAPI geometry value.</param>
        /// <returns></returns>
        protected override byte[] FromGeometry(object value)
        {
            IGeometry geometry = value as IGeometry;
            if (geometry == null)
            {
                return null;
            }

            SetDefaultSRID(geometry);

            var sqlServerBytesWriter = new SqlServerBytesWriter();
            return sqlServerBytesWriter.Write(geometry);
        }

        /// <summary>
        /// Converts to GeoAPI geometry type from database geometry type.
        /// </summary>
        /// <param name="value">The databse geometry value.</param>
        /// <returns></returns>
        protected override IGeometry ToGeometry(object value)
        {
            var bytes = value as byte[];
            if (bytes == null)
            {
                return null;
            }

            var sqlServerBytesReader = new SqlServerBytesReader();
            IGeometry geometry = sqlServerBytesReader.Read(bytes);
            SetDefaultSRID(geometry);
            return geometry;
        }

        private static byte[] ToByteArray(string hex)
        {
            if (hex.Length % 2 == 1)
                throw new ArgumentException("Invalid input. It must have an even length.", "hex");

            byte[] data = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                char c1 = hex[i];
                char c2 = hex[i + 1];

                int result = (c1 < 'A') ? (c1 - '0') : (10 + (c1 - 'A'));
                result = result << 4;
                result |= (c2 < 'A') ? (c2 - '0') : (10 + (c2 - 'A'));
                data[i / 2] = (byte)(result);
            }
            return data;
        }

        private static string ToString(byte[] bytes)
        {
            char[] data = new char[bytes.Length * 2];
            int idx = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                int n1 = bytes[i] >> 4;
                int n2 = bytes[i] & 0xF;
                data[idx++] = (char)((n1) < 10 ? '0' + n1 : n1 - 10 + 'A');
                data[idx++] = (char)((n2) < 10 ? '0' + n2 : n2 - 10 + 'A');
            }
            return new string(data);
        }

        [Serializable]
        private class CustomGeometryType : MutableType
        {
            internal CustomGeometryType() : base(new BinarySqlType())
            {
            }

            public override object Get(DbDataReader rs, int index, ISessionImplementor session)
            {
                // Npgsql 3 from the received bytes creates his own PostGisGeometry type.
                // As we need to return a byte array that represents the geometry object,
                // we will retrive the bytes from the reader instead.
                var length = (int)rs.GetBytes(index, 0, null, 0, 0);
                var buffer = new byte[length];
                if (length > 0)
                {
                    rs.GetBytes(index, 0, buffer, 0, length);
                }
                return buffer;
            }

            public override object Get(DbDataReader rs, string name, ISessionImplementor session)
            {
                return Get(rs, rs.GetOrdinal(name), session);
            }

            [Obsolete("This method has no more usages and will be removed in a future version. Override ToLoggableString instead.")]
            public override string ToString(object val)
            {
                return string.Empty;
                //return PostGisGeometryType.ToString((byte[])val);
            }

            [Obsolete("This method has no more usages and will be removed in a future version.")]
            public override object FromStringValue(string xml)
            {
                return ToByteArray(xml);
            }

            public override System.Type ReturnedClass => typeof(IGeometry);

            public override void Set(DbCommand cmd, object value, int index, ISessionImplementor session)
            {
                SqlParameter sqlParameter = (SqlParameter)cmd.Parameters[index];
                sqlParameter.SqlDbType = SqlDbType.Binary;
                //sqlParameter.UdtTypeName = "geometry";
                sqlParameter.Value = value;
            }

            public override string Name => ReturnedClass.Name;

            public override object DeepCopyNotNull(object value)
            {
                var arr = (byte[])value;
                return arr.Clone();
            }
        }
    }
}