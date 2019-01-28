Imports System.Net
Imports System.Net.Sockets
Imports Comms
Imports Comms.DataStructures

Public Module Module1
    '
    'Test Class to implement EncryptX
    'Created by: NMGod
    'Email: nmgod@nmgod.com
    '
    'Uses converted functions from Aluigi's encryptX method to pull down
    'data from server and use custom structures
    '
    '    Copyright 2011,2012 NMGod
    '
    '    This program is free software; you can redistribute it and/or modify
    '    it under the terms of the GNU General Public License as published by
    '    the Free Software Foundation; either version 2 of the License, or
    '    (at your option) any later version.
    '
    '    This program is distributed in the hope that it will be useful,
    '    but WITHOUT ANY WARRANTY; without even the implied warranty of
    '    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    '    GNU General Public License for more details.
    '
    '    You should have received a copy of the GNU General Public License
    '    along with this program; if not, write to the Free Software
    '    Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307 USA
    '
    '    http://www.gnu.org/licenses/gpl-2.0.txt
    '

    Public Sub Main()

        Dim sList As ServerList = RefreshList()
        If Not Object.Equals(Nothing, sList) Then
            IO.File.WriteAllText(My.Computer.FileSystem.SpecialDirectories.Desktop & "\Data.txt", sList.ToString)
        End If
        End
    End Sub


    Private Function RefreshList() As ServerList
        Dim tcp As New Net.Sockets.TcpClient
        Dim gamestr As String = "tribesv"

        tcp.Connect("localhost", 28910)
        'tcp.Connect("65.112.87.186", 28910)

        Dim validate As Byte() = New Byte(8) {}

        Enctypex.enctypex_decoder_rand_validate(validate)

        Dim info As String = "\mapname\numplayers\maxplayers\hostname\hostport\gametype\gamever\password\gamename\gamemode\gamevariant\trackingstats\dedicated\minver"
        'Dim info As String = "\hostname\gametype\gamemode\numplayers\maxplayers\password\gamevariant"

        Dim packet() As Byte = Enctypex.compileXPacket(gamestr, validate, info, 1)

        tcp.ReceiveBufferSize = 3472
        tcp.GetStream.Write(packet, 0, packet.Length)

        Do
            System.Threading.Thread.Sleep(100)
        Loop Until tcp.Available > 0

        Dim response() As Byte = New Byte() {}
        Dim rlen As Integer = 0
        tcp.ReceiveTimeout = 10

        While (True)
            Dim temp() As Byte = New Byte(response.Length - 1) {}
            Array.Copy(response, temp, response.Length)
            response = New Byte(temp.Length + tcp.ReceiveBufferSize - 1) {}
            Array.Copy(temp, response, temp.Length)
            Try
                rlen += tcp.GetStream.Read(response, rlen, tcp.ReceiveBufferSize)
            Catch ex As IO.IOException
                temp = New Byte(response.Length - 1) {}
                Array.Copy(response, temp, response.Length)
                response = New Byte(rlen - 1) {}
                Array.Copy(temp, response, rlen)
                Exit While
            End Try
        End While

        tcp.Close()

        Dim encx_data As Enctypex_data_t = Nothing
        Dim decoded_data() As Byte = enctypex_decoder(s2b("y3D28k"), validate, response, rlen, encx_data)

        Return New ServerList(decoded_data)
    End Function
End Module
