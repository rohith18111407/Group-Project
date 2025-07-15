import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { Observable } from "rxjs";

@Injectable({ providedIn: 'root' })
export class StatisticsService {
  private baseUrl = 'http://localhost:5239/api/v1/statistics';

  constructor(private http: HttpClient) {}

   private getAuthHeaders(): HttpHeaders {
        const token = sessionStorage.getItem('jwtToken');
        return new HttpHeaders({ 'Authorization': `Bearer ${token}` });
    }

  getFileExtensionStats(): Observable<any> {
    return this.http.get(`${this.baseUrl}/file-extension-stats`,  { headers: this.getAuthHeaders() });
  }

  getUploadDownloadTrends(): Observable<any> {
    return this.http.get(`${this.baseUrl}/upload-download-trends`,  { headers: this.getAuthHeaders() });
  }

  getRecentActivities(): Observable<any> {
    return this.http.get(`${this.baseUrl}/recent-activities`,  { headers: this.getAuthHeaders() });
  }

  getRecentFiles(): Observable<any> {
    return this.http.get(`${this.baseUrl}/recent-files`,  { headers: this.getAuthHeaders() });
  }

  getRecentItems(): Observable<any> {
    return this.http.get(`${this.baseUrl}/recent-items`,  { headers: this.getAuthHeaders() });
  }
}