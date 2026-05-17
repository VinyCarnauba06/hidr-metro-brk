import 'dart:convert';
import 'package:sqflite/sqflite.dart';
import 'package:path/path.dart';

// Armazenamento local para funcionar offline
class StorageService {
  static Database? _db;

  static Future<Database> get db async {
    _db ??= await _init();
    return _db!;
  }

  static Future<Database> _init() async {
    final path = join(await getDatabasesPath(), 'hidrometro_local.db');
    return openDatabase(
      path,
      version: 2,
      onCreate: (db, version) async {
        await db.execute(_schemaSql);
      },
      onUpgrade: (db, oldVersion, newVersion) async {
        if (oldVersion < 2) {
          // Adiciona colunas de resiliência de sync sem recriar a tabela
          await db.execute('ALTER TABLE leituras_pendentes ADD COLUMN retentativas INTEGER DEFAULT 0');
          await db.execute('ALTER TABLE leituras_pendentes ADD COLUMN erro_ultimo TEXT');
          await db.execute('ALTER TABLE leituras_pendentes ADD COLUMN sincronizado_em TEXT');
        }
      },
    );
  }

  static const _schemaSql = '''
    CREATE TABLE leituras_pendentes (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      os_id INTEGER NOT NULL,
      unidade_id INTEGER NOT NULL,
      numero_unidade TEXT,
      foto_path TEXT,
      valor_m3 REAL,
      valor_litros INTEGER DEFAULT 0,
      origem TEXT DEFAULT 'ia',
      criado_em TEXT NOT NULL,
      sincronizado INTEGER DEFAULT 0,
      sincronizado_em TEXT,
      retentativas INTEGER DEFAULT 0,
      erro_ultimo TEXT
    )
  ''';

  static Future<int> salvarLeituraPendente({
    required int osId,
    required int unidadeId,
    String? numeroUnidade,
    String? fotoPath,
    double? valorM3,
    int valorLitros = 0,
    String origem = 'ia',
  }) async {
    final database = await db;
    return database.insert('leituras_pendentes', {
      'os_id': osId,
      'unidade_id': unidadeId,
      'numero_unidade': numeroUnidade,
      'foto_path': fotoPath,
      'valor_m3': valorM3,
      'valor_litros': valorLitros,
      'origem': origem,
      'criado_em': DateTime.now().toIso8601String(),
      'sincronizado': 0,
    });
  }

  static Future<List<Map<String, dynamic>>> listarPendentes() async {
    final database = await db;
    return database.query('leituras_pendentes', where: 'sincronizado = 0');
  }

  static Future<void> marcarSincronizado(int id) async {
    final database = await db;
    await database.update(
      'leituras_pendentes',
      {
        'sincronizado': 1,
        'sincronizado_em': DateTime.now().toIso8601String(),
        'erro_ultimo': null,
      },
      where: 'id = ?',
      whereArgs: [id],
    );
  }

  // Registra falha sem apagar o registro — permite nova tentativa futura.
  static Future<void> marcarErroSync(int id, String erro) async {
    final database = await db;
    await database.rawUpdate(
      '''UPDATE leituras_pendentes
         SET retentativas = retentativas + 1,
             erro_ultimo  = ?
         WHERE id = ?''',
      [erro, id],
    );
  }

  // Retorna pendentes com menos de maxRetentativas falhas (evita fila travada por item corrompido).
  static Future<List<Map<String, dynamic>>> listarPendentesParaSync({
    int maxRetentativas = 5,
  }) async {
    final database = await db;
    return database.query(
      'leituras_pendentes',
      where: 'sincronizado = 0 AND retentativas < ?',
      whereArgs: [maxRetentativas],
      orderBy: 'criado_em ASC',
    );
  }
}
