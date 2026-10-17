// SaveFolderActivity.java
// Remake-only: the save folder option on Android (Data.SaveFolder). Scoped storage keeps every app out of the others'
// Android/data folders, so a folder that file managers and sync tools (Syncthing) can reach has to come from the Storage
// Access Framework: this see-through activity opens the system's folder picker (ACTION_OPEN_DOCUMENT_TREE), keeps the
// read / write grant across restarts (takePersistableUriPermission) and finishes; the game polls the static fields
// (state 0 waiting, 1 picked (resultUri / resultName), 2 cancelled, 3 failed (error)). The static helpers below read and
// write the folder's files through the ContentResolver (the tree's documents are no plain paths), as UTF-8 text: the
// saves are JSON. No permission in the manifest is needed. Registered in the unityLibrary manifest by the Editor's
// AndroidModImportActivity.

package com.joppietoppie.gof2remake;

import android.app.Activity;
import android.content.ContentResolver;
import android.content.Context;
import android.content.Intent;
import android.content.UriPermission;
import android.database.Cursor;
import android.net.Uri;
import android.os.Bundle;
import android.provider.DocumentsContract;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;

public class SaveFolderActivity extends Activity {
    static final int REQUEST = 0x6f2e;

    public static volatile int state;
    public static volatile String resultUri, resultName, error;

    /** Opens the folder picker. */
    public static void start(Activity from) {
        state = 0;
        resultUri = null;
        resultName = null;
        error = null;
        from.startActivity(new Intent(from, SaveFolderActivity.class));
    }

    @Override
    protected void onCreate(Bundle saved) {
        super.onCreate(saved);
        if (saved != null) return;   // recreated (a turned screen): the picker's result still comes here
        Intent pick = new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE);
        pick.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION
                    | Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION | Intent.FLAG_GRANT_PREFIX_URI_PERMISSION);
        try {
            startActivityForResult(pick, REQUEST);
        } catch (Exception e) {
            fail("no folder picker on this device (" + e.getMessage() + ")");
        }
    }

    @Override
    protected void onActivityResult(int request, int result, Intent data) {
        super.onActivityResult(request, result, data);
        if (request != REQUEST) return;
        Uri tree = result == RESULT_OK && data != null ? data.getData() : null;
        if (tree == null) {
            state = 2;
            finish();
            return;
        }
        try {
            getContentResolver().takePersistableUriPermission(tree,
                    Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION);
            resultName = displayName(this, tree.toString());
            resultUri = tree.toString();
            state = 1;
        } catch (Exception e) {
            fail(e.getMessage());
            return;
        }
        finish();
    }

    void fail(String message) {
        error = message != null ? message : "unknown error";
        state = 3;
        finish();
    }

    // ---- the folder's files --------------------------------------------------------------------------------------

    static Uri children(Uri tree) {
        return DocumentsContract.buildChildDocumentsUriUsingTree(tree, DocumentsContract.getTreeDocumentId(tree));
    }

    /** A readable name for the folder: "Documents/GoF2" for the shared storage, else its display name. */
    public static String displayName(Context context, String treeUri) {
        Uri tree = Uri.parse(treeUri);
        String id = DocumentsContract.getTreeDocumentId(tree);
        if ("com.android.externalstorage.documents".equals(tree.getAuthority()) && id.indexOf(':') >= 0) {
            String volume = id.substring(0, id.indexOf(':')), path = id.substring(id.indexOf(':') + 1);
            String where = "primary".equals(volume) ? "" : volume + ": ";
            return where + (path.length() > 0 ? path : "/");
        }
        Uri doc = DocumentsContract.buildDocumentUriUsingTree(tree, id);
        try (Cursor c = context.getContentResolver().query(doc, new String[] { DocumentsContract.Document.COLUMN_DISPLAY_NAME }, null, null, null)) {
            if (c != null && c.moveToFirst() && !c.isNull(0)) return c.getString(0);
        } catch (Exception ignored) {
        }
        return id;
    }

    /** Whether the game still holds its grant on the folder (a deleted folder or a revoked grant loses it). */
    public static boolean granted(Context context, String treeUri) {
        for (UriPermission p : context.getContentResolver().getPersistedUriPermissions())
            if (p.getUri().toString().equals(treeUri) && p.isReadPermission() && p.isWritePermission()) return true;
        return false;
    }

    /** Lets the grant go (another folder or the default chosen). */
    public static void release(Context context, String treeUri) {
        try {
            context.getContentResolver().releasePersistableUriPermission(Uri.parse(treeUri),
                    Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION);
        } catch (Exception ignored) {
        }
    }

    /** The folder's files, one per line: name TAB size TAB last modified (ms). Throws when the folder can't be read. */
    public static String list(Context context, String treeUri) throws Exception {
        Uri tree = Uri.parse(treeUri);
        StringBuilder sb = new StringBuilder();
        try (Cursor c = context.getContentResolver().query(children(tree), new String[] {
                DocumentsContract.Document.COLUMN_DISPLAY_NAME, DocumentsContract.Document.COLUMN_SIZE,
                DocumentsContract.Document.COLUMN_LAST_MODIFIED, DocumentsContract.Document.COLUMN_MIME_TYPE }, null, null, null)) {
            if (c == null) throw new Exception("the folder can't be read");
            while (c.moveToNext()) {
                if (DocumentsContract.Document.MIME_TYPE_DIR.equals(c.getString(3))) continue;
                sb.append(c.getString(0)).append('\t').append(c.isNull(1) ? -1 : c.getLong(1)).append('\t')
                  .append(c.isNull(2) ? 0 : c.getLong(2)).append('\n');
            }
        }
        return sb.toString();
    }

    static Uri find(ContentResolver resolver, Uri tree, String name) {
        try (Cursor c = resolver.query(children(tree), new String[] {
                DocumentsContract.Document.COLUMN_DOCUMENT_ID, DocumentsContract.Document.COLUMN_DISPLAY_NAME }, null, null, null)) {
            if (c == null) return null;
            while (c.moveToNext())
                if (name.equals(c.getString(1))) return DocumentsContract.buildDocumentUriUsingTree(tree, c.getString(0));
        }
        return null;
    }

    /** A file's text, null when it isn't there. Throws when it can't be read. */
    public static String read(Context context, String treeUri, String name) throws Exception {
        ContentResolver resolver = context.getContentResolver();
        Uri doc = find(resolver, Uri.parse(treeUri), name);
        if (doc == null) return null;
        try (InputStream in = resolver.openInputStream(doc)) {
            if (in == null) throw new Exception(name + " can't be read");
            ByteArrayOutputStream out = new ByteArrayOutputStream();
            byte[] buffer = new byte[1 << 16];
            int n;
            while ((n = in.read(buffer)) > 0) out.write(buffer, 0, n);
            return new String(out.toByteArray(), StandardCharsets.UTF_8);
        }
    }

    /** Writes a file (made when it isn't there, else replaced). Throws when it can't be written. */
    public static void write(Context context, String treeUri, String name, String text) throws Exception {
        ContentResolver resolver = context.getContentResolver();
        Uri tree = Uri.parse(treeUri);
        Uri doc = find(resolver, tree, name);
        if (doc == null) {
            Uri parent = DocumentsContract.buildDocumentUriUsingTree(tree, DocumentsContract.getTreeDocumentId(tree));
            doc = DocumentsContract.createDocument(resolver, parent, "application/octet-stream", name);
            if (doc == null) throw new Exception(name + " can't be made in the folder");
        }
        try (OutputStream out = resolver.openOutputStream(doc, "wt")) {
            if (out == null) throw new Exception(name + " can't be written");
            out.write(text.getBytes(StandardCharsets.UTF_8));
        }
    }

    /** Deletes a file; true when it is gone (or wasn't there). */
    public static boolean delete(Context context, String treeUri, String name) throws Exception {
        ContentResolver resolver = context.getContentResolver();
        Uri doc = find(resolver, Uri.parse(treeUri), name);
        return doc == null || DocumentsContract.deleteDocument(resolver, doc);
    }
}
