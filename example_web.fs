\ Language: ESP32FORTH
\ Program for simple web interface 
\ Author: Vaclav Poselt October 2023
defined? MARKER 0<> [if] forget MARKER [then]
create MARKER
only also httpd also streams also internals

\ next is definition of helping word mvbar	 
0 value mystr       \ addres of string block
0 value mystr#      \ string block position?
0 value mystrn      \ string block lenght
: mystr-init ( -- ) \ activate 10 bytes buffer
    10 dup allocate ( 10 addr ior )
    throw ( 10 addr )
    to mystr to mystrn 0 to mystr# ; \ addr to mystr, 10 to mystrn, 0 to mystr#
: mystr-grow ( -- ) \ increase buffer to (size+1)*2    
    mystrn 1+ 2* to mystrn mystr mystrn resize throw to mystr ;
: +mystr ( ch -- ) \ add char to buffer 
    mystr# mystrn >= if mystr-grow then  \ increase buffer if necessary
    mystr mystr# + c! 1 +to mystr# ;     \ put char to next position in buffer
: 1tch   begin >in @ #tib @ >= while refill drop nl +mystr repeat ;
: tch ( -- ch ) tib >in @ + c@ ;
: vbar? ( -- f ) 1tch tch 124 = ;  \ 124 is code for vertical bar symbol
: m$ mystr-init
     begin vbar? 0= while tch +mystr 1 >in +! repeat 1 >in +!
     mystr mystr# ;
: mvbar ( comp: -- <string|> | exec: addr len) \ create temporary counted string
    m$ state @ if swap [ internals ] aliteral aliteral then ; immediate 
\ end of definition of mvbar |

3000 stream webintstream                \ buffer for user web interface content
\ where 1st cell is total lenght of buffer, 2nd cell no of written bytes, 4rth
\ cell is start of buffer content 
: webcontent    ( -- addr len )         \ prepare values for html send
    webintstream dup 3 cells +          \ gives addr of first byte
    swap cell+ @                        \ gives no of written bytes
;
: webcontentreset ( -- )                \ reset stream
    webintstream  cell+ 0 swap !        \ reset no of written bytes
;

0 value GPIO26  \ staus of GPIO26, 0-ON, 1-off
0 value GPIO27  \ staus of GPIO27, 0-ON, 1-off
\ pins GPIO26. 27 can be used for control something

: htmlpage      \ create whole html page in webintstream
webcontentreset \ reset html page buffer
mvbar <!DOCTYPE html>
<html>
<head>
<meta name="viewport" content="width=device-width, initial-scale=1">
<link rel="icon" href="data:,">
<!-- CSS to style the on/off buttons --> 
<!-- Feel free to change the background-color and font-size attributes to fit your preferences -->
<style>
html { font-family: Verdana, Helvetica; display: inline-block; margin: 0px auto; text-align: center;
background-color: #CCFFCC}
.button { background-color: #4CAF50; border: 2px solid blue; border-radius: 12px; 
color: white; padding: 16px 40px; text-decoration: none; font-size: 30px; margin: 2px;
cursor: pointer;}
.button2 {background-color: #FF3300;}
</style>
</head>
<body><h1>ESP32FORTH Web Server</h1> | webintstream >stream  \ common part of HTML page
            
\ Display current state, and ON/OFF buttons for GPIO 26 and 27
mvbar <p><b>GPIO 26 STATUS is: | webintstream >stream
GPIO26 str webintstream >stream    \ show value of GPIO26
mvbar &nbsp; &nbsp; &nbsp; GPIO 27 STATUS is: | webintstream >stream
GPIO27 str webintstream >stream    \ show value of GPIO27
mvbar </b></p> | webintstream >stream
\ display buttons in color acording  value
GPIO26 1 = if   \ if OFF show ON button else OFF button
          mvbar <p><a href="/26/on"><button class="button">GPIO26</button></a>
                | webintstream >stream
          else
           mvbar <p><a href="/26/off"><button class="button button2">GPIO26</button></a>
             | webintstream >stream 
           then


GPIO27 1 = if   \ if OFF show ON button else OFF button
          mvbar <a href="/27/on"><button class="button">GPIO27</button></a></p>
                | webintstream >stream
          else
           mvbar <a href="/27/off"><button class="button button2">GPIO27</button></a></p>
             | webintstream >stream 
           then                         

mvbar
<form action="/get">
  <label for="txt">Input text max. 30 chars: </label>
  <input type="text" id="txt" name="TX" size="30">
  <input type="submit" value="Submit">
</form><br>            
<form action="/get">
  <label for="num">Number (between 1 and 10): </label>
  <input type="number" id="num" name="NO" min="1" max="10" size="4">
  <input type="submit" value="Submit">
</form><br>           
<form action="/get">
  <label for="vol">Volume (between 0 and 100): </label>
  <input type="range" name="RG" value="4" min="0" max="100" id="vol" 
  onchange="document.getElementById('ran').innerText = this.value" >
  <input type="submit" value="Submit">
  <br> value: <span id="ran"></span>  
</form><br>
<form action="/get">
  <label for="appt">Select a time:</label>
  <input type="time" id="appt" name="TM">
  <input type="submit" value="Submit">
</form><br>
 <form action="/get">
  <label for="dat">Birthday:</label>
  <input type="date" id="dat" name="DT">
  <input type="submit" value="Submit">
</form><br>
<form  action="/get">
  <label for="cars">Choose a car:</label>
  <select id="cars" name="CA">
    <option value="volvo">Volvo</option>
    <option value="saab">Saab</option>
    <option value="fiat">Fiat</option>
    <option value="audi">Audi</option>
  </select>
  <input type="submit" value="Submit">
</form><br> 
<button type="button"
onclick="document.getElementById('demo').innerHTML = Date()">
Click me to display Date and Time.</button><br>
<p id="demo"></p>
</body>
</html>  
   | webintstream >stream     \ last part of html page saved to webintstream
;

: htmlpagesend  \ send whole html page
    s" text/html" ok-response
    htmlpage    \ create html page in webintstream buffer
    webcontent send    \ and send it to client
;               
create mypad 8 allot           \ create 8 bytes buffer
\ respond  will analyze given path and decode returned values from html forms
\ with possible code for actions, now there is only print of values
: respond  ( addr len-- )       
    dup 50 min 8 >              \ len is min 9 chars or more, for. ex. /get?IN=x
    if over     ( addr len addr --)
      mypad  8 cmove      \ first 8 chars to mypad
      swap 8 + swap 8 - ( addr+8 len-8--) \ from 9th char of path is returned value    
      s" /get?TX=" mypad 8 str=    \ it is text value 
        if cr ." text value:"  type then 
      s" /get?NO=" mypad 8 str=    \ it is number
        if cr ." number value:"  s>number? drop . then
      s" /get?RG=" mypad 8 str=    \ it is range value
        if cr ." range value:"  s>number? drop . then
      s" /get?TM=" mypad 8 str=    \ it is time value as 22%3A39=22:39
        if cr ." time value:" drop dup 2 type [char] : emit 5 + 2 type then
      s" /get?DT=" mypad 8 str=    \ it is date value
        if cr ." date value:"  type then
      s" /get?CA=" mypad 8 str=    \ it is select value 
        if cr ." select value:"  type then
      then
;         

: serve-page  ( --)               \ simple parsing and action of client respond
  path s" /" str= if htmlpagesend exit then \ exit leaves from serve-page
  path s" /26/on" str= if cr ." ACTION for  /26/on " cr \ here put action word
   0 to GPIO26 htmlpagesend exit then
  path s" /26/off" str= if cr ." ACTION for /26/off " cr
   1 to GPIO26 htmlpagesend exit then
    path s" /27/on" str= if cr ." ACTION for /27/on " cr
   0 to GPIO27 htmlpagesend exit then
  path s" /27/off" str= if cr ." ACTION for /27/off " cr
   1 to GPIO27 htmlpagesend exit then
  path respond      \ actions for html forms
  htmlpagesend exit  \ resend html page
;

: runpage  begin handleClient if serve-page 100 ms then 500 ms again ;
: connect z" your SSID" z" your psw" login 80 server ;

' runpage 100 100 task webtask
: runServer ( -- ) connect webtask start-task ; \ 