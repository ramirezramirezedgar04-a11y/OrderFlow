import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'config.dart';

void main()=>runApp(const OrderFlowApp());
class OrderFlowApp extends StatelessWidget{
 const OrderFlowApp({super.key});
 @override Widget build(BuildContext context)=>MaterialApp(
  debugShowCheckedModeBanner:false,title:'OrderFlow',
  theme:ThemeData(colorScheme:ColorScheme.fromSeed(seedColor:const Color(0xFFE87535)),useMaterial3:true),
  home:const OrdersPage());
}
class OrdersPage extends StatefulWidget{const OrdersPage({super.key});@override State<OrdersPage> createState()=>_OrdersPageState();}
class _OrdersPageState extends State<OrdersPage>{
 List<dynamic> orders=[];bool loading=true;String? error;
 Future<void> load() async{
  setState(()=>{loading=true,error=null});
  try{
   final r=await http.get(Uri.parse('${AppConfig.apiBaseUrl}/api/orders')).timeout(const Duration(seconds:10));
   if(r.statusCode!=200)throw Exception('HTTP ${r.statusCode}');
   setState(()=>{orders=jsonDecode(r.body) as List<dynamic>,loading=false});
  }catch(e){setState(()=>{error='No se pudo conectar con la API. Revisa la URL del servidor. $e',loading=false});}
 }
 @override void initState(){super.initState();load();}
 String label(String s)=>({'Pending':'Pendiente','Preparing':'En preparación','Ready':'Listo','Delivered':'Entregado','Cancelled':'Cancelado'}[s]??s);
 @override Widget build(BuildContext context)=>Scaffold(
  appBar:AppBar(title:const Text('OrderFlow · Comandas'),actions:[IconButton(onPressed:load,icon:const Icon(Icons.refresh))]),
  body:RefreshIndicator(onRefresh:load,child:loading?const Center(child:CircularProgressIndicator()):
   error!=null?ListView(children:[const SizedBox(height:100),Padding(padding:const EdgeInsets.all(24),child:Text(error!,textAlign:TextAlign.center))]):
   orders.isEmpty?ListView(children:[const SizedBox(height:120),Center(child:Text('Aún no hay comandas.'))]):
   ListView.builder(padding:const EdgeInsets.all(12),itemCount:orders.length,itemBuilder:(context,index){
    final o=orders[index] as Map<String,dynamic>;final items=(o['items'] as List<dynamic>?) ?? [];
    return Card(child:Padding(padding:const EdgeInsets.all(16),child:Column(crossAxisAlignment:CrossAxisAlignment.start,children:[
     Text('Comanda #${o['id']} · Mesa ${o['tableNumber']}',style:const TextStyle(fontWeight:FontWeight.bold)),
     Text(label(o['status']??'')),const Divider(),
     ...items.map((i)=>Text('${i['quantity']} × ${i['productName']}')),
     Align(alignment:Alignment.centerRight,child:Text('Total: \$${o['total']}',style:const TextStyle(fontWeight:FontWeight.bold)))
    ])));
   }))
 );
}
