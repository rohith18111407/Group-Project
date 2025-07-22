import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { map, Observable } from "rxjs";

@Injectable({ providedIn: 'root' })
export class AdminService {
    private readonly baseUrl = 'http://localhost:5239/api/v1';

    constructor(private http: HttpClient) { }

    private getAuthHeaders(): HttpHeaders {
        const token = sessionStorage.getItem('jwtToken');
        return new HttpHeaders({ 'Authorization': `Bearer ${token}` });
    }

    getUserProfile(): Observable<any> {
        return this.http.get<any>(`${this.baseUrl}/auth/me`, { headers: this.getAuthHeaders() }).pipe(
            map(res => res.data)
        );
    }

    getAllItems(sortBy: 'createdAt' | 'createdBy' | 'name' | 'category' = 'createdAt', isDescending: boolean = false): Observable<any[]> {
        const params = new URLSearchParams({
            isDescending: isDescending.toString(),
            pageNumber: '1',
            pageSize: '100',
            sortBy
        });

        return this.http.get<any>(`${this.baseUrl}/Items?${params.toString()}`, { headers: this.getAuthHeaders() })
            .pipe(map(res => res.data));
    }

    createItem(item: any): Observable<any> {
        return this.http.post(`${this.baseUrl}/items`, item, { headers: this.getAuthHeaders() });
    }

    deleteItem(id: string): Observable<any> {
        return this.http.delete(`${this.baseUrl}/items/${id}`, { headers: this.getAuthHeaders() });
    }

    updateItem(id: string, payload: { name: string; description: string; categories: string[] }): Observable<any> {
        return this.http.put(`${this.baseUrl}/items/${id}`, payload, { headers: this.getAuthHeaders() });
    }

    getAllUsers(): Observable<any[]> {
        return this.http.get<any>(`${this.baseUrl}/Users?pageNumber=1&pageSize=100`, { headers: this.getAuthHeaders() })
            .pipe(map(res => res.data));
    }

    createUser(user: { username: string; password: string; roles: string[] }): Observable<any> {
        return this.http.post(`${this.baseUrl}/users`, user, { headers: this.getAuthHeaders() });
    }

    deleteUser(id: string): Observable<any> {
        return this.http.delete(`${this.baseUrl}/Users/${id}`, { headers: this.getAuthHeaders() });
    }

    updateUser(id: string, payload: { username: string; roles: string[] }): Observable<any> {
        return this.http.put(`${this.baseUrl}/users/${id}`, payload, { headers: this.getAuthHeaders() });
    }

    getAllFiles(): Observable<any[]> {
        return this.http.get<any>(`${this.baseUrl}/files`, { headers: this.getAuthHeaders() })
            .pipe(map(res => res.data));
    }

    uploadFile(fileData: FormData): Observable<any> {
        return this.http.post(`${this.baseUrl}/files/upload`, fileData, { headers: this.getAuthHeaders() });
    }

    deleteFile(id: string) {
        return this.http.delete<any>(`http://localhost:5239/api/v1/files/${id}`, { headers: this.getAuthHeaders() });
    }

    downloadFile(filename: string, version: number) {
        const url = `${this.baseUrl}/files/${filename}/v${version}`;
        return this.http.get(url, { headers: this.getAuthHeaders(), responseType: 'blob' });
    }

    // ARCHIVE METHODS
    
    /**
     * Get all archived files
     */
    getArchivedFiles(): Observable<any[]> {
        return this.http.get<any>(`${this.baseUrl}/archive/files`, { headers: this.getAuthHeaders() })
            .pipe(map(res => res.data));
    }

    /**
     * Get archived files by admin name
     */
    getArchivedFilesByAdmin(adminName: string): Observable<any[]> {
        return this.http.get<any>(`${this.baseUrl}/archive/files/by-admin/${adminName}`, { headers: this.getAuthHeaders() })
            .pipe(map(res => res.data));
    }

    /**
     * Unarchive a file (Admin only)
     */
    unarchiveFile(fileId: string): Observable<any> {
        return this.http.post<any>(`${this.baseUrl}/archive/files/${fileId}/unarchive`, {}, { headers: this.getAuthHeaders() });
    }

    /**
     * Archive a file manually (Admin only)
     */
    archiveFileManually(fileId: string, reason: string): Observable<any> {
        return this.http.post<any>(`${this.baseUrl}/archive/files/${fileId}/archive`, { reason }, { headers: this.getAuthHeaders() });
    }

    /**
     * Get archival statistics
     */
    getArchivalStats(): Observable<any> {
        return this.http.get<any>(`${this.baseUrl}/archive/stats`, { headers: this.getAuthHeaders() })
            .pipe(map(res => res.data));
    }

    /**
     * Trigger inactive admin archival manually
     */
    triggerInactiveAdminArchival(inactiveDaysThreshold?: number): Observable<any> {
        const payload = inactiveDaysThreshold ? { inactiveDaysThreshold } : {};
        return this.http.post<any>(`${this.baseUrl}/archive/admin-inactivity/trigger`, payload, { headers: this.getAuthHeaders() });
    }

    /**
     * Preview inactive admin archival
     */
    previewInactiveAdminArchival(inactiveDaysThreshold: number = 30): Observable<any> {
        return this.http.get<any>(`${this.baseUrl}/archive/admin-inactivity/preview?inactiveDaysThreshold=${inactiveDaysThreshold}`, { headers: this.getAuthHeaders() })
            .pipe(map(res => res.data));
    }

    /**
     * Get inactive admins
     */
    getInactiveAdmins(inactiveDaysThreshold: number = 30): Observable<any> {
        return this.http.get<any>(`${this.baseUrl}/archive/admin-inactivity/inactive-admins?inactiveDaysThreshold=${inactiveDaysThreshold}`, { headers: this.getAuthHeaders() })
            .pipe(map(res => res.data));
    }

    /**
     * Trigger general inactivity archival
     */
    triggerGeneralInactivityArchival(inactiveDaysThreshold?: number): Observable<any> {
        const payload = inactiveDaysThreshold ? { inactiveDaysThreshold } : {};
        return this.http.post<any>(`${this.baseUrl}/archive/general-inactivity/trigger`, payload, { headers: this.getAuthHeaders() });
    }

    /**
     * Bulk unarchive files (Admin only) - Frontend implementation
     * Note: This uses individual unarchive calls until backend bulk endpoint is implemented
     */
    bulkUnarchiveFiles(fileIds: string[]): Observable<any> {
        // For now, we'll handle bulk operations on the frontend
        // In the future, you can add a backend endpoint for bulk operations
        return new Observable(observer => {
            const results = { success: 0, failed: 0, total: fileIds.length };
            let completed = 0;

            fileIds.forEach(fileId => {
                this.unarchiveFile(fileId).subscribe({
                    next: (response) => {
                        results.success++;
                        completed++;
                        if (completed === fileIds.length) {
                            observer.next(results);
                            observer.complete();
                        }
                    },
                    error: (error) => {
                        results.failed++;
                        completed++;
                        if (completed === fileIds.length) {
                            observer.next(results);
                            observer.complete();
                        }
                    }
                });
            });
        });
    }
}